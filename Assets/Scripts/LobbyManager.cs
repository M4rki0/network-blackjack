using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Netcode.Transports.UTP;

public class LobbyManager : NetworkBehaviour
{
    public TMP_InputField ipInputField;
    public GameObject startGameButton;
    public GameObject joinCodePanel;
    public TMP_Text joinCodeText;

    async void Start()
    {
        await UnityServices.InitializeAsync();
        await AuthenticationService.Instance.SignInAnonymouslyAsync();
        NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        startGameButton.SetActive(false);
    }
    
    public async void HostGame()
    {
        Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
        string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
        
        Debug.Log("Join code: " + joinCode);
        joinCodeText.text = joinCode;
        joinCodePanel.SetActive(true);
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new Unity.Networking.Transport.Relay.RelayServerData(allocation, "dtls"));
        
        NetworkManager.Singleton.StartHost();
    }

    public async void JoinGame()
    {
        JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(ipInputField.text);
        
        UnityTransport transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
        transport.SetRelayServerData(new Unity.Networking.Transport.Relay.RelayServerData(joinAllocation, "dtls"));
        NetworkManager.Singleton.StartClient();
    }

    public void CopyJoinCode()
    {
        GUIUtility.systemCopyBuffer = joinCodeText.text;
    }

    public void Close()
    {
        joinCodePanel.SetActive(false);
    }

    void OnClientConnected(ulong clientId)
    {
        Debug.Log("Client connected: " + clientId);
        if (IsHost)
        {
            startGameButton.SetActive(true);
        }
    }

    public void StartGame()
    {
        NetworkManager.Singleton.SceneManager.LoadScene("Game", LoadSceneMode.Single);
    }
}
