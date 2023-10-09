﻿using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using VkNet.AudioBypassService.Abstractions;
using VkNet.AudioBypassService.Abstractions.Categories;
using VkNet.Extensions.DependencyInjection;

namespace VkNet.AudioBypassService.Utils;

public class TokenRefreshHandler : ITokenRefreshHandler
{
    private readonly IExchangeTokenStore _exchangeTokenStore;
    private readonly IServiceProvider _serviceProvider;
    private readonly IVkTokenStore _tokenStore;

    public TokenRefreshHandler(IExchangeTokenStore exchangeTokenStore, IServiceProvider serviceProvider, IVkTokenStore tokenStore)
    {
        _exchangeTokenStore = exchangeTokenStore;
        _serviceProvider = serviceProvider;
        _tokenStore = tokenStore;
    }

    public async Task<string> RefreshTokenAsync(string oldToken)
    {
        if (await _exchangeTokenStore.GetExchangeTokenAsync() is not { } exchangeToken)
            return null;

        var authCategory = _serviceProvider.GetRequiredService<IAuthCategory>();

        var (token, expiresIn) = await authCategory.RefreshTokensAsync(oldToken, exchangeToken);

        await _tokenStore.SetAsync(token, DateTimeOffset.Now + TimeSpan.FromSeconds(expiresIn));

        var (newExchangeToken, _) = await authCategory.GetExchangeToken();
        
        await _exchangeTokenStore.SetExchangeTokenAsync(newExchangeToken);

        return token;
    }
}