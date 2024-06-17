using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NBXplorer.Configuration;
using NBXplorer.Events;
using NBXplorer.Models;
using RabbitMQ.Client;

namespace NBXplorer.MessageBrokers
{
    public class BrokerHostedService : IHostedService
    {
        private readonly EventAggregator _eventAggregator;
        private readonly IOptions<ExplorerConfiguration> _options;
        private readonly NBXplorerNetworkProvider _networkProvider;
        private readonly ILogger<BrokerHostedService> _logger;
        private readonly IBrokerClient _brokerClient;

        public BrokerHostedService(
            EventAggregator eventAggregator,
            IOptions<ExplorerConfiguration> options,
            NBXplorerNetworkProvider networkProvider,
            ILogger<BrokerHostedService> logger,
            ILogger<RabbitMqBroker> rabbitMqLogger)
        {
            _eventAggregator = eventAggregator;
            _options = options;
            _networkProvider = networkProvider;
            _logger = logger;
            _brokerClient = new RabbitMqBroker(networkProvider, new ConnectionFactory
            {
                HostName = options.Value.RabbitMqHostName,
                VirtualHost = options.Value.RabbitMqVirtualHost,
                UserName = options.Value.RabbitMqUsername,
                Password = options.Value.RabbitMqPassword
            }, options.Value.RabbitMqTransactionExchange, options.Value.RabbitMqBlockExchange, rabbitMqLogger);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _eventAggregator.Subscribe<NewTransactionEvent>(async e => await _brokerClient.Send(e));
            _eventAggregator.Subscribe<NewBlockEvent>(async e => await _brokerClient.Send(e));
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return _brokerClient.Close();
        }
    }
}
