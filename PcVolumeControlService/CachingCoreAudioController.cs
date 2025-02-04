﻿using AudioSwitcher.AudioApi.CoreAudio;

namespace PcVolumeControlService;

public class CachingCoreAudioController : IDisposable
{
    private static readonly TimeSpan MinimumCacheLifetime = TimeSpan.FromMinutes(1);
    private static readonly object CoreAudioControllerLock = new();
    private static readonly TimeSpan CacheExpiryTolerance = TimeSpan.FromSeconds(1);

    private readonly ILogger<CachingCoreAudioController> _logger;
    private readonly TimeSpan _cacheLifetime;
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
                GC.SuppressFinalize(this);
            }
        }
    }

    // ReSharper disable once MemberCanBePrivate.Global
    public TimeSpan CacheLifetime
    {
        // ReSharper disable once UnusedMember.Global
        get => _cacheLifetime;
        init
        {
            if(value < MinimumCacheLifetime)
                throw new ArgumentOutOfRangeException($"Cannot set lifetime smaller than {MinimumCacheLifetime}");

            _cacheLifetime = value;
            _logger.LogTrace("Cache lifetime set to {cacheLifetime}", _cacheLifetime);
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

        _logger.LogTrace(
            "Cache expiry updated from {previousCacheExpiry} to {cacheExpiry}", previousCacheExpiry, _cacheExpiry);

        ExpireCache(stoppingToken);
    }

    private void ExpireCache(CancellationToken stoppingToken)
    {
        // Expire cache in the background.
        Task.Run(async () =>
        {
            _logger.LogTrace("Waiting to expire cache.");
            var now = DateTime.Now;
            if(_cacheExpiry > now)
            {
                await Task.Delay(_cacheExpiry.Subtract(now), stoppingToken);
                if(stoppingToken.IsCancellationRequested)
                    return;
            }

            _logger.LogTrace("Checking if cache should be expired.");
            bool expireCache;
            lock(CoreAudioControllerLock)
            {
                var cacheExpiryTimeSpan = _cacheExpiry.Subtract(DateTime.Now).Subtract(CacheExpiryTolerance);
                expireCache = cacheExpiryTimeSpan <= TimeSpan.Zero;

                if(expireCache)
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
            if(expireCache)
                _logger.LogTrace("Expired cache.");
        }, stoppingToken).ConfigureAwait(false);
    }
}
