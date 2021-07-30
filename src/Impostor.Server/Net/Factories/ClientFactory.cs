﻿using System;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Microsoft.Extensions.DependencyInjection;

namespace Impostor.Server.Net.Factories
{
    internal class ClientFactory<TClient> : IClientFactory
        where TClient : ClientBase
    {
        private readonly IServiceProvider _serviceProvider;

        public ClientFactory(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        public ClientBase Create(IHazelConnection connection, string name, int clientVersion, Language language, QuickChatModes chatMode)
        {
            var client = ActivatorUtilities.CreateInstance<TClient>(_serviceProvider, name, clientVersion, language, chatMode, connection);
            connection.Client = client;
            return client;
        }
    }
}
