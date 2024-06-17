using System.Threading.Tasks;
using NBXplorer.Models;

namespace NBXplorer.MessageBrokers
{
    public interface IBrokerClient
    {
        Task Send(NewTransactionEvent transactionEvent);
        Task Send(NewBlockEvent blockEvent);
        Task Close();
    }
}
