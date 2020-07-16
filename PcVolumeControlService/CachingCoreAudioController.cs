﻿using System;
using System.Threading;
using System.Threading.Tasks;
using AudioSwitcher.AudioApi.CoreAudio;
using Microsoft.Extensions.Logging;

namespace PcVolumeControlService
{
    public class CachingCoreAudioController : IDisposable
    {
        private static readonly TimeSpan MinimumCacheLifetime = TimeSpan.FromMinutes(1);
        private static readonly object CoreAudioControllerLock = new object();

        private readonly ILogger<CachingCoreAudioController> _logger;
        private TimeSpan _cacheLifetime;
        private DateTime _cacheExpiry;
        private CoreAudioController _coreAudioController;

        public CachingCoreAudioController(ILogger<CachingCoreAudioController> logger)
        {
            _logger = logger;
            CacheLifetime = MinimumCacheLifetime;
        }

        public void Dispose()
        {
            lock(CoreAudioControllerLock)
            {
                try
                {
                    _coreAudioController?.Dispose();
                }
                finally
                {
                    _coreAudioController = null;
                }
            }
        }

        // ReSharper disable once MemberCanBePrivate.Global
        public TimeSpan CacheLifetime
        {
            // ReSharper disable once UnusedMember.Global
            get => _cacheLifetime;
            set
            {
                if(value < MinimumCacheLifetime)
                    throw new ArgumentOutOfRangeException($"Cannot set lifetime smaller than {MinimumCacheLifetime}");

                _cacheLifetime = value;
                // ReSharper disable once InconsistentlySynchronizedField
                _logger.LogInformation($"Cache lifetime set to {_cacheLifetime}");
            }
        }

        public CoreAudioController GetCoreAudioController(CancellationToken stoppingToken)
        {
            lock(CoreAudioControllerLock)
            {
                _coreAudioController ??= new CoreAudioController();
                UpdateCacheExpiry(stoppingToken);

                return _coreAudioController;
            }
        }

        private void UpdateCacheExpiry(CancellationToken stoppingToken)
        {
            var previousCacheExpiry = _cacheExpiry;
            _cacheExpiry = DateTime.Now.Add(_cacheLifetime);

            _logger.LogInformation($"Cache expiry updated from {previousCacheExpiry} to {_cacheExpiry}");

            ExpireCache(stoppingToken);
        }

        private void ExpireCache(CancellationToken stoppingToken)
        {
            Task.Run(async () =>
            {
                // Wait for the right time to expire cache.
                _logger.LogInformation("Waiting to expire cache.");
                var now = DateTime.Now;
                if(_cacheExpiry > now)
                    await Task.Delay(_cacheExpiry.Subtract(now), stoppingToken);

                _logger.LogInformation("Checking if cache should be expired.");
                lock(CoreAudioControllerLock)
                {
                    if(_cacheExpiry <= DateTime.Now)
                    {
                        _logger.LogInformation("Expiring cache.");
                        try
                        {
                            _coreAudioController?.Dispose();
                        }
                        finally
                        {
                            _coreAudioController = null;
                        }
                    }
                }
            }, stoppingToken).ConfigureAwait(false);
        }
    }
}