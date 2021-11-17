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
                    SendMessageToClient(ServerToClientSignifiers.AccountCreationFailed + "", id); 
                }

                else if(!nameInUse)
                {
                    //Name not in use, create account & add to list.
                    PlayerAccount newAccount = new PlayerAccount(n, p, int.Parse(gameID));
                    playerAccounts.AddLast(newAccount);
                    SendMessageToClient(ServerToClientSignifiers.AccountCreationComplete + "", id);
                }
            }

            else if(accountSignifier == ClientToServerAccountSignifiers.Login)
            {
                Debug.Log("Login to account");
                string n = csv[2];
                string p = csv[3];
                string gameID = csv[4];
                bool accountFound = false;

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
                            break;
                        }
                    }
                }

                //Account found, proceed to login to game room specified.
                if(accountFound)
                {

                }

                //Account not found, login failed.
                else if (!accountFound)
                {

                }
            }
        }
       
    }

  
}

public class PlayerAccount
{
    public string username;
    public string password;
    public int userID;

    public PlayerAccount(string name, string pass, int idNum)
    {
        username = name;
        password = pass;
        userID = idNum;
    }
}

public static class ClientToServerStateSignifiers
{
    public const int Account = 1;

    public const int Game = 2;

    public const int Spectate = 3;
}

public static class ClientToServerAccountSignifiers
{
    public const int CreateAccount = 1;

    public const int Login = 2;
}

public static class ServerToClientSignifiers
{
    public const int LoginComplete = 1;

    public const int LoginFailed = 2;

    public const int AccountCreationComplete = 3;

    public const int AccountCreationFailed = 4;
}

