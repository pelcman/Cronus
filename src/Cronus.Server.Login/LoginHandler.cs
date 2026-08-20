using Cronus.Common;
using Cronus.Network;
using Cronus.Network.Packets;

namespace Cronus.Server.Login;

/// <summary>
/// Handles login-stage packets for one connection (ports <c>PacketHandler_Login</c> +
/// <c>ReqCLogin</c>, JMS v186 path). Currently implements <c>CP_CheckPassword</c>: parse the
/// credential packet, authenticate, and reply with <c>LP_CheckPasswordResult</c>.
/// </summary>
public sealed class LoginHandler : PacketHandlerBase
{
    private readonly OpcodeTable _clientOps;
    private readonly LoginService _loginService;
    private readonly LoginPackets _packets;
    private readonly int _checkPasswordOpcode;

    public LoginHandler(
        OpcodeTable clientOpcodes,
        OpcodeTable serverOpcodes,
        LoginService loginService,
        ServerConfig config)
    {
        _clientOps = clientOpcodes;
        _loginService = loginService;
        _packets = new LoginPackets(serverOpcodes, config);
        _checkPasswordOpcode = clientOpcodes.Get(ClientOpcode.CheckPassword);
    }

    public override async ValueTask OnPacketAsync(MapleSession session, int opcode, PacketReader packet)
    {
        if (opcode == _checkPasswordOpcode)
        {
            await HandleCheckPasswordAsync(session, packet).ConfigureAwait(false);
        }

        // Other login opcodes (world info, character list, ...) land in later milestones.
    }

    private async ValueTask HandleCheckPasswordAsync(MapleSession session, PacketReader packet)
    {
        // JMS v131..302 CP_CheckPassword layout (pre-KMS160/JMS308):
        //   [mapleId:str][password:str][machineId:16][unk1:int][unk2:byte][unk3:byte]
        string mapleId = packet.ReadString();
        string password = packet.ReadString();
        // Remaining machine-id / flag fields are parsed for correctness but unused for now.
        if (packet.Remaining >= 16)
        {
            packet.Skip(16); // machine id
        }

        LoginService.Outcome outcome = _loginService.Authenticate(mapleId, password);

        byte[] response = outcome is { Result: LoginResult.Success, Account: { } account }
            ? _packets.CheckPasswordSuccess(account)
            : _packets.CheckPasswordFailure(outcome.Result);

        session.UserData = outcome.Account;
        await session.SendAsync(response).ConfigureAwait(false);
    }
}
