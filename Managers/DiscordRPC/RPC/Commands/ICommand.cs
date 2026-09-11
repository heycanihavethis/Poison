using Poison.Managers.DiscordRPC.RPC.Payload;

namespace Poison.Managers.DiscordRPC.RPC.Commands
{
    internal interface ICommand
    {
        IPayload PreparePayload(long nonce);
    }
}
