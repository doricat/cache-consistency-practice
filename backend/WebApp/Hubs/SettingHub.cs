using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;

namespace WebApp.Hubs
{
    public class SettingHub : Hub<ISettingHub>
    {
        public async Task SetDelay(int delay)
        {
            if (delay < 0)
            {
                return;
            }

            await Clients.Others.ReceiveDelay(delay);
        }
    }

    public interface ISettingHub
    {
        Task ReceiveDelay(int delay);
    }
}