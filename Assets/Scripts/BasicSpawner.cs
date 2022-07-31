using System;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.SceneManagement;

using Fusion;
using Fusion.Sockets;

/// <summary>
/// Description
/// </summary>
public class BasicSpawner : MonoBehaviour, INetworkRunnerCallbacks
{
	[SerializeField]
	private NetworkPrefabRef _playerPrefab;
	private Dictionary<PlayerRef, NetworkObject> _spawnedCharacters = new Dictionary<PlayerRef, NetworkObject>();

	private NetworkRunner _runner;

	private bool _mouseButton0;
	private bool _mouseButton1;

	private void OnGUI()
	{
		if (_runner == null)
		{
			if (GUI.Button(new Rect(0, 0, 200, 40), "Host"))
			{
				StartGame(GameMode.Host);
			}
			if (GUI.Button(new Rect(0, 40, 200, 40), "Join"))
			{
				StartGame(GameMode.Client);
			}
		}
	}

	private void Update()
	{
		_mouseButton0 = _mouseButton0 | Input.GetMouseButton(0); // 클릭 여부를 여기서 감지
		_mouseButton1 = _mouseButton1 || Input.GetMouseButton(1);
	}

	async void StartGame(GameMode mode)
	{
		// Fusion Runner을 생성하고, 입력을 받을것이라고 지정
		// (그냥 Local에서 입력을 받아도 되지만, Host에서 무시 될 수 있음)
		_runner = gameObject.AddComponent<NetworkRunner>();
		_runner.ProvideInput = true;

		// 버튼 누른거에 따라 Host/Join으로 게임을 시작함
		await _runner.StartGame(new StartGameArgs()
		{
			GameMode = mode,
			SessionName = "TestRoom",
			Scene = SceneManager.GetActiveScene().buildIndex,
			SceneManager = gameObject.AddComponent<NetworkSceneManagerDefault>()
		});
	}

	public void OnPlayerJoined(NetworkRunner runner, PlayerRef player)
	{
		// 플레이어 고유 좌표 지정 후 생성
		Vector3 spawnPosition = new Vector3((player.RawEncoded % runner.Config.Simulation.DefaultPlayers) * 3, 1, 0);
		NetworkObject networkPlayerObject = runner.Spawn(_playerPrefab, spawnPosition, Quaternion.identity, player);

		// 게임을 떠날 때 GameObject를 제거하기 위해 정보를 담음
		_spawnedCharacters.Add(player, networkPlayerObject);
	}

	public void OnPlayerLeft(NetworkRunner runner, PlayerRef player)
	{
		// 대상 플레이어의 GameObject를 제거
		if (_spawnedCharacters.TryGetValue(player, out NetworkObject networkObject))
		{
			runner.Despawn(networkObject);
			_spawnedCharacters.Remove(player);
		}
	}
	public void OnInput(NetworkRunner runner, NetworkInput input)
	{
		// 입력을 받아서 NetworkInput으로 변환
		var data = new NetworkInputData();

		if (Input.GetKey(KeyCode.W))
			data.direction += Vector3.forward;

		if (Input.GetKey(KeyCode.S))
			data.direction += Vector3.back;

		if (Input.GetKey(KeyCode.A))
			data.direction += Vector3.left;

		if (Input.GetKey(KeyCode.D))
			data.direction += Vector3.right;

		if (_mouseButton0)
			data.buttons |= NetworkInputData.MOUSEBUTTON1;
		_mouseButton0 = false;

		if (_mouseButton1)
			data.buttons |= NetworkInputData.MOUSEBUTTON2;
		_mouseButton1 = false;

		input.Set(data);
	}
	public void OnInputMissing(NetworkRunner runner, PlayerRef player, NetworkInput input) { }
	public void OnConnectedToServer(NetworkRunner runner) { }
	public void OnDisconnectedFromServer(NetworkRunner runner) { }
	public void OnConnectRequest(NetworkRunner runner, NetworkRunnerCallbackArgs.ConnectRequest request, byte[] token) { }
	public void OnConnectFailed(NetworkRunner runner, NetAddress remoteAddress, NetConnectFailedReason reason) { }
	public void OnUserSimulationMessage(NetworkRunner runner, SimulationMessagePtr message) { }
	public void OnSessionListUpdated(NetworkRunner runner, List<SessionInfo> sessionList) { }
	public void OnCustomAuthenticationResponse(NetworkRunner runner, Dictionary<string, object> data) { }

	/* https://doc.photonengine.com/ko-KR/fusion/current/manual/host-migration */

	// Step 1.
	// It happens on the Photon Cloud and there is no direct relation with the code on the peers.

	// Step 2.
	// OnHostMigration callback
	public async void OnHostMigration(NetworkRunner runner, HostMigrationToken hostMigrationToken)
	{
		// Step 3.
		// Shutdown the current Runner, this will not be used anymore. Perform any prior setup and tear down of the old Runner

		// The new "ShutdownReason.HostMigration" can be used here to inform why it's being shut down in the "OnShutdown" callback
		await runner.Shutdown(destroyGameObject: false, shutdownReason: ShutdownReason.HostMigration);

		// Step 4.
		// Create a new Runner.
		//var newRunner = Instantiate(_runnerPrefab);
		var newRunner = gameObject.AddComponent<NetworkRunner>();
		newRunner.ProvideInput = true;

		// setup the new runner...

		// Start the new Runner using the "HostMigrationToken" and pass a callback ref in "HostMigrationResume".
		StartGameResult result = await newRunner.StartGame(new StartGameArgs()
		{
			// SessionName = SessionName,              // ignored, peer never disconnects from the Photon Cloud
			// GameMode = gameMode,                    // ignored, Game Mode comes with the HostMigrationToken
			HostMigrationToken = hostMigrationToken,   // contains all necessary info to restart the Runner
			HostMigrationResume = HostMigrationResume, // this will be invoked to resume the simulation
													   // other args
		});

		// Check StartGameResult as usual
		if (!result.Ok)
		{
			Debug.LogWarning(result.ShutdownReason);
		}
		else
		{
			Debug.Log("Done");
		}
	}
	// Step 5.
	// Resume Simulation on the new Runner
	void HostMigrationResume(NetworkRunner runner)
	{
		// Get a temporary reference for each NO from the old Host
		foreach (var resumeNO in runner.GetResumeSnapshotNetworkObjects())
		{
			if (
				// Extract any NetworkBehavior used to represent the position/rotation of the NetworkObject
				// this can be either a NetworkTransform or a NetworkRigidBody, for example
				resumeNO.TryGetBehaviour<NetworkPositionRotation>(out var posRot))
			{

				runner.Spawn(resumeNO, position: posRot.ReadPosition(), rotation: posRot.ReadRotation(), onBeforeSpawned: (runner, newNO) =>
				{
					// One key aspects of the Host Migration is to have a simple way of restoring the old NetworkObjects state
					// If all state of the old NetworkObject is all what is necessary, just call the NetworkObject.CopyStateFrom
					newNO.CopyStateFrom(resumeNO);

					// and/or

					// If only partial State is necessary, it is possible to copy it only from specific NetworkBehaviours
					/*if (resumeNO.TryGetBehaviour<NetworkBehaviour>(out var myCustomNetworkBehaviour))
					{
						newNO.GetComponent<NetworkBehaviour>().CopyStateFrom(myCustomNetworkBehaviour);
					}*/
				});
			}
		}
	}
	public void OnShutdown(NetworkRunner runner, ShutdownReason shutdownReason)
	{
		Debug.LogError(shutdownReason);

		// Can check if the Runner is being shutdown because of the Host Migration
		if (shutdownReason == ShutdownReason.HostMigration)
		{
			// ...
		}
		else
		{
			// Or a normal Shutdown
		}
	}
	public void OnReliableDataReceived(NetworkRunner runner, PlayerRef player, ArraySegment<byte> data) { }
	public void OnSceneLoadDone(NetworkRunner runner) { }
	public void OnSceneLoadStart(NetworkRunner runner) { }
}