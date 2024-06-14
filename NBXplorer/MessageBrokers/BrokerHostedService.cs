﻿using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Azure.ServiceBus;
using NBXplorer.Configuration;
using RabbitMQ.Client;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace NBXplorer.MessageBrokers
{
    public class BrokerHostedService : IHostedService
    {
        private readonly EventAggregator _EventAggregator;
        private bool _Disposed = false;
        private readonly CompositeDisposable _subscriptions = new CompositeDisposable();
        private IBrokerClient _senderBlock = null;
        private IBrokerClient _senderTransactions = null;
        private readonly ExplorerConfiguration _config;
        private readonly ILogger<BrokerHostedService> _logger;
        private readonly ILogger<RabbitMqBroker> _rabbitLogger;

        public BrokerHostedService(
            EventAggregator eventAggregator,
            IOptions<ExplorerConfiguration> config,
            NBXplorerNetworkProvider networks,
            ILogger<BrokerHostedService> logger,
            ILogger<RabbitMqBroker> rabbitLogger) // Ensure this is public
        {
            _EventAggregator = eventAggregator;
            Networks = networks;
            _config = config.Value;
            _logger = logger;
            _rabbitLogger = rabbitLogger;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (_Disposed)
                throw new ObjectDisposedException(nameof(BrokerHostedService));

            _senderBlock = CreateClientBlock();
            _senderTransactions = CreateClientTransaction();

            _subscriptions.Add(_EventAggregator.Subscribe<Models.NewBlockEvent>(async o =>
            {
                await _senderBlock.Send(o);
            }));

            _subscriptions.Add(_EventAggregator.Subscribe<Models.NewTransactionEvent>(async o =>
            {
                await _senderTransactions.Send(o);
            }));
            return Task.CompletedTask;
        }

        IBrokerClient CreateClientTransaction()
        {
            var brokers = new List<IBrokerClient>();
            if (!string.IsNullOrEmpty(_config.AzureServiceBusConnectionString))
            {
                if (!string.IsNullOrWhiteSpace(_config.AzureServiceBusTransactionQueue))
                    brokers.Add(CreateAzureQueue(_config.AzureServiceBusConnectionString, _config.AzureServiceBusTransactionQueue));
                if (!string.IsNullOrWhiteSpace(_config.AzureServiceBusTransactionTopic))
                    brokers.Add(CreateAzureTopic(_config.AzureServiceBusConnectionString, _config.AzureServiceBusTransactionTopic));
            }
            if (!string.IsNullOrEmpty(_config.RabbitMqHostName) && 
                !string.IsNullOrEmpty(_config.RabbitMqUsername) && 
                !string.IsNullOrEmpty(_config.RabbitMqPassword)) 
            {
                if (!string.IsNullOrEmpty(_config.RabbitMqTransactionExchange)) 
                {
                    brokers.Add(CreateRabbitMqExchange(
                        hostName: _config.RabbitMqHostName,
                        virtualHost: _config.RabbitMqVirtualHost,
                        username: _config.RabbitMqUsername,
                        password: _config.RabbitMqPassword,
                        newTransactionExchange: _config.RabbitMqTransactionExchange,
                        newBlockExchange: string.Empty));
                }
            }
            return new CompositeBroker(brokers);
        }

        IBrokerClient CreateClientBlock()
        {
            var brokers = new List<IBrokerClient>();
            if (!string.IsNullOrEmpty(_config.AzureServiceBusConnectionString))
            {
                if (!string.IsNullOrWhiteSpace(_config.AzureServiceBusBlockQueue))
                    brokers.Add(CreateAzureQueue(_config.AzureServiceBusConnectionString, _config.AzureServiceBusBlockQueue));
                if (!string.IsNullOrWhiteSpace(_config.AzureServiceBusBlockTopic))
                    brokers.Add(CreateAzureTopic(_config.AzureServiceBusConnectionString, _config.AzureServiceBusBlockTopic));
            }

            if (!string.IsNullOrEmpty(_config.RabbitMqHostName) && 
                !string.IsNullOrEmpty(_config.RabbitMqUsername) && 
                !string.IsNullOrEmpty(_config.RabbitMqPassword)) 
            {
                if (!string.IsNullOrEmpty(_config.RabbitMqBlockExchange)) 
                {
                    brokers.Add(CreateRabbitMqExchange(
                        hostName: _config.RabbitMqHostName,
                        virtualHost: _config.RabbitMqVirtualHost,
                        username: _config.RabbitMqUsername,
                        password: _config.RabbitMqPassword,
                        newTransactionExchange: string.Empty,
                        newBlockExchange: _config.RabbitMqBlockExchange));
                }
            }
            return new CompositeBroker(brokers);
        }

        private IBrokerClient CreateAzureQueue(string connnectionString, string queueName)
        {
            return new AzureBroker(new QueueClient(connnectionString, queueName), Networks);
        }

        private IBrokerClient CreateAzureTopic(string connectionString, string topicName)
        {
            return new AzureBroker(new TopicClient(connectionString, topicName), Networks);
        }

        private IBrokerClient CreateRabbitMqExchange(
            string hostName, string virtualHost, 
            string username, string password, 
            string newTransactionExchange, string newBlockExchange)
        {
            return new RabbitMqBroker(
                Networks,
                new ConnectionFactory() { 
                    HostName = hostName, VirtualHost = virtualHost,
                    UserName = username, Password = password }, 
                newTransactionExchange, newBlockExchange,
                _rabbitLogger); // Pass the logger
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            if (_senderBlock is null)
                return;
            _Disposed = true;
            _subscriptions.Dispose();
            await Task.WhenAll(_senderBlock.Close(), _senderTransactions.Close());
        }

        public NBXplorerNetworkProvider Networks { get; }
    }
}
