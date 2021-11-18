using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using System.IO;
using UnityEngine.UIElements;

public class NetworkedServer : MonoBehaviour
{
    int maxConnections = 1000;
    int reliableChannelID;
    int unreliableChannelID;
    int hostID;
    int socketPort = 5491;

    LinkedList<PlayerAccount> playerAccounts;

    PlayerAccount temp1;
    PlayerAccount temp2;
    PlayerAccount temp3;

    GameRoom room1;
    GameRoom room2;
    GameRoom room3;

    bool room1InUse = false;
    bool room2InUse = false;
    bool room3InUse = false;

    // Start is called before the first frame update
    void Start()
    { 
        NetworkTransport.Init();
        ConnectionConfig config = new ConnectionConfig();
        reliableChannelID = config.AddChannel(QosType.Reliable);
        unreliableChannelID = config.AddChannel(QosType.Unreliable);
        HostTopology topology = new HostTopology(config, maxConnections);
        hostID = NetworkTransport.AddHost(topology, socketPort, null);

        playerAccounts = new LinkedList<PlayerAccount>();
    }

    // Update is called once per frame
    void Update()
    {
        int recHostID;
        int recConnectionID;
        int recChannelID;
        byte[] recBuffer = new byte[1024];
        int bufferSize = 1024;
        int dataSize;
        byte error = 0;

        NetworkEventType recNetworkEvent = NetworkTransport.Receive(out recHostID, out recConnectionID, out recChannelID, recBuffer, bufferSize, out dataSize, out error);

        switch (recNetworkEvent)
        {
            case NetworkEventType.Nothing:
                break;
            case NetworkEventType.ConnectEvent:
                Debug.Log("Connection, " + recConnectionID);
                break;
            case NetworkEventType.DataEvent:
                string msg = Encoding.Unicode.GetString(recBuffer, 0, dataSize);
                ProcessRecievedMsg(msg, recConnectionID);
                break;
            case NetworkEventType.DisconnectEvent:
                Debug.Log("Disconnection, " + recConnectionID);
                break;
        }
    }

    public void SendMessageToClient(string msg, int id)
    {
        byte error = 0;
        byte[] buffer = Encoding.Unicode.GetBytes(msg);
        NetworkTransport.Send(hostID, id, reliableChannelID, buffer, msg.Length * sizeof(char), out error);
    }

    private void ProcessRecievedMsg(string msg, int id)
    {
        Debug.Log("msg recieved = " + msg + ".  connection id = " + id);

        //Split message by comma, allowing signifiers to be read.
        string[] csv = msg.Split(',');

        int stateSignifier = int.Parse(csv[0]);

        //Create new account.
        if(stateSignifier == ClientToServerStateSignifiers.Account)
        {
            int accountSignifier = int.Parse(csv[1]);

            if(accountSignifier == ClientToServerAccountSignifiers.CreateAccount)
            {
                Debug.Log("Create Account");

                string n = csv[2];
                string p = csv[3];
                string gameID = csv[4];
                bool nameInUse = false;

                foreach(PlayerAccount pa in playerAccounts)
                {
                    if (pa.username == n)
                    {
                        nameInUse = true;
                        break;
                    }
                }

                if(nameInUse)
                {
                    //Name in use, account cannot be created.
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.AccountCreationFailed + "", id); 
                }

                else if(!nameInUse)
                {
                    //Name not in use, create account & add to list.
                    PlayerAccount newAccount = new PlayerAccount(n, p, int.Parse(gameID), id);
                    playerAccounts.AddLast(newAccount);
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.AccountCreationComplete + "", id);
                }
            }

            else if(accountSignifier == ClientToServerAccountSignifiers.Login)
            {
                Debug.Log("Login to account");
                string n = csv[2]; //name
                string p = csv[3]; //password
                string g = csv[4]; //game ID
                bool accountFound = false;

                PlayerAccount thisPlayer = null;

                //Check for login info match.
                foreach(PlayerAccount pa in playerAccounts)
                {
                    //Check for username match.
                    if(pa.username == n)
                    {
                        //Check for password match.
                        if(pa.password == p)
                        {
                            //Username and password match. Login
                            accountFound = true;
                            thisPlayer = pa;
                            break;
                        }
                    }
                }

                //Account found, proceed to login to game room specified.
                if(accountFound)
                {
                    Debug.Log("Account Found");
                    SendMessageToClient(ServerToClientStateSignifiers.Account + "," + ServerToClientAccountSignifiers.LoginComplete + "", id);

                    if(g == "1")
                    {
                        if(temp1 == null)
                        {
                            //Player 1 connects. Wait for player 2.
                            temp1 = thisPlayer;
                            thisPlayer = null;
                        }
                        else if(temp1 != null && !room1InUse)
                        {
                            //Player 2 connects. Begin game.
                            room1 = new GameRoom(temp1, thisPlayer);
                            //Initialize game for P1 and P2
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.GameInitialize, temp1.playerID);
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.GameInitialize, thisPlayer.playerID);
                            //Notify P1 that it's their turn.
                            SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.CurrentTurn + "," + room1.TopLeft + "," + room1.TopMiddle + "," + room1.TopRight + "," + room1.MiddleLeft + "," + room1.Middle + "," + room1.MiddleRight + "," + room1.BottomLeft + "," + room1.BottomMiddle + "," + room1.BottomRight + "", temp1.playerID);
                            temp1 = null;
                            thisPlayer = null;
                            room1InUse = true;
                        }
                        else if(temp1 != null && room1InUse)
                        {
                            //Player is spectator for room 1.
                        }
                    }
                    else if (g == "2")
                    {

                    }
                    else if (g == "3")
                    {

                    }
                }

                //Account not found, login failed.
                else if (!accountFound)
                {
                    Debug.Log("Account Not Found");
                }
            }
        }
        else if(stateSignifier == ClientToServerStateSignifiers.Game)
        {
            int gameSignifier = int.Parse(csv[1]);

            if(gameSignifier == ClientToServerGameSignifiers.ChoiceMade)
            {
                int choice = int.Parse(csv[2]);

                if(room1InUse) //Check room 1.
                {
                    //Player1 (X)
                    if(room1.P1Turn && room1.player1.playerID == id)
                    {
                        Debug.Log("G1: Player 1 chose square " + choice);
                        UpdateBoard(room1, choice, 1);
                        ChangeTurn(room1.player2, room1);
                    }
                    else if(room1.P1Turn == false && room1.player2.playerID == id) //Player2 (O)
                    {
                        Debug.Log("G1: Player 2 chose square " + choice);
                        UpdateBoard(room1, choice, 2);
                        ChangeTurn(room1.player1, room1);
                    }
                }
                if(room2InUse) //Check room 2.
                {
                    //Player1 (X)
                    if(room2.P1Turn && room2.player1.playerID == id)
                    {
                        Debug.Log("G2: Player 1 chose square " + choice);
                        UpdateBoard(room2, choice, 1);
                        ChangeTurn(room2.player2, room2);
                    }
                    else if(room2.P1Turn == false && room2.player2.playerID == id) //Player2 (O)
                    {
                        Debug.Log("G2: Player 2 chose square " + choice);
                        UpdateBoard(room2, choice, 2);
                        ChangeTurn(room2.player1, room2);
                    }
                }
                if(room3InUse) //Check room 3.
                {
                    //Player1 (X)
                    if(room3.P1Turn && room3.player1.playerID == id)
                    {
                        Debug.Log("G3: Player 1 chose square " + choice);
                        UpdateBoard(room3, choice, 1);
                        ChangeTurn(room3.player2, room3);
                    }
                    else if(room3.P1Turn == false && room3.player2.playerID == id) //Player2 (O)
                    {
                        Debug.Log("G3: Player 2 chose square " + choice);
                        UpdateBoard(room3, choice, 2);
                        ChangeTurn(room3.player1, room3);
                    }
                }
            }
            
        }
    }

    public void GameLoop(GameRoom room)
    {
        //Player1 (X)
        if(room.P1Turn)
        {

        }
        else //Player2 (O)
        {

        }
    }

    public void UpdateBoard(GameRoom room, int choice, int playerNumber)
    {
        if (choice == 1) //Top Left
        {
            room.TopLeft.status = playerNumber;
        }
        else if (choice == 2) //Top Middle
        {
            room.TopMiddle.status = playerNumber;
        }
        else if (choice == 3) //Top Right
        {
            room.TopRight.status = playerNumber;
        }
        else if (choice == 4) //Middle Left
        {
            room.MiddleLeft.status = playerNumber;
        }
        else if (choice == 5) //Middle
        {
            room.Middle.status = playerNumber;
        }
        else if (choice == 6) //Middle Right
        {
            room.MiddleRight.status = playerNumber;
        }
        else if (choice == 7) //Bottom Left
        {
            room.BottomLeft.status = playerNumber;
        }
        else if (choice == 8) //Bottom Middle
        {
            room.BottomMiddle.status = playerNumber;
        }
        else if (choice == 9) //BottomRight
        {
            room.BottomRight.status = playerNumber;
        }
    }

    //Alternate player turns.
    public void ChangeTurn(PlayerAccount swapTo, GameRoom room)
    {
        SendMessageToClient(ServerToClientStateSignifiers.Game + "," + ServerToClientGameSignifiers.CurrentTurn + "," + room.TopLeft + "," + room.TopMiddle + "," + room.TopRight + "," + room.MiddleLeft + "," + room.Middle + "," + room.MiddleRight + "," + room.BottomLeft + "," + room.BottomMiddle + "," + room.BottomRight + "", swapTo.playerID);
        room.P1Turn = !room.P1Turn;
    }

  
}

public class PlayerAccount
{
    public string username;
    public string password;
    public int gameID;
    public int playerID;

    public PlayerAccount(string name, string pass, int gameIDNum, int playerIDNum)
    {
        username = name;
        password = pass;
        gameID = gameIDNum;
        playerID = playerIDNum;
    }
}

public class GameRoom
{
    public PlayerAccount player1;
    public PlayerAccount player2;

    public bool P1Turn;

    public GameTile TopLeft;
    public GameTile TopMiddle;
    public GameTile TopRight;
    public GameTile MiddleLeft;
    public GameTile Middle;
    public GameTile MiddleRight;
    public GameTile BottomLeft;
    public GameTile BottomMiddle;
    public GameTile BottomRight;

    public GameRoom(PlayerAccount xPlayer, PlayerAccount oPlayer)
    {
        player1 = xPlayer;

        player2 = oPlayer;

        P1Turn = true;

        TopLeft.status = 0;
        TopMiddle.status = 0;
        TopRight.status = 0;
        MiddleLeft.status = 0;
        Middle.status = 0;
        MiddleRight.status = 0;
        BottomLeft.status = 0;
        BottomMiddle.status = 0;
        BottomRight.status = 0;
    }

   

}

public class GameTile
{
    //Status
    //0 = Unclaimed
    //1 = X
    //2 = O
    public int status;
}

public static class ClientToServerStateSignifiers
{
    public const int Account = 1;

    public const int Game = 2;

    public const int Spectate = 3;

    public const int Other = 9;
}

public static class ClientToServerAccountSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;
}

public static class ClientToServerGameSignifiers
{
    public const int ChoiceMade = 1;
}

public static class ServerToClientStateSignifiers
{
    public const int Account = 1;

    public const int Game = 2;
}

public static class ServerToClientAccountSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;
}

public static class ServerToClientGameSignifiers
{
    public const int CurrentTurn = 1;

    public const int Player1Won = 2;

    public const int Player2Won = 3;

    public const int GameInitialize = 9;

}

