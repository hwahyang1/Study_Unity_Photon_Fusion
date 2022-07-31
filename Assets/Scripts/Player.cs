using System.Collections;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;

using Fusion;

/// <summary>
/// Description
/// </summary>
public class Player : NetworkBehaviour
{
	[SerializeField]
	private Ball _prefabBall;
	[SerializeField]
	private PhysxBall _prefabPhysxBall;

	[Networked]
	private TickTimer delay { get; set; }
	private Vector3 _forward;

	[Networked(OnChanged = nameof(OnBallSpawned))]
	public NetworkBool spawned { get; set; } // NetworkBool 대신 byte나 int를 사용하면 확실함, 대신 대역폭을 더 먹게 됨

	private NetworkCharacterControllerPrototype _cc;
	private Material _material;
	Material material
	{
		get
		{
			if (_material == null)
				_material = GetComponentInChildren<MeshRenderer>().material;
			return _material;
		}
	}

	private Text _messages;

	[Rpc(RpcSources.InputAuthority, RpcTargets.All)] // 플레이어 입력에 대한 권한이 있는 클라이언트만 이 메소드를 호출하고, 모든 클라이언트에서 실행하게 만듦
	public void RPC_SendMessage(string message, RpcInfo info = default)
	{
		if (_messages == null)
			_messages = FindObjectOfType<Text>();
		if (info.Source == Runner.Simulation.LocalPlayer)
			message = $"You said: {message}\n";
		else
			message = $"Some other player said: {message}\n";
		_messages.text += message;
	}

	private void Awake()
	{
		_cc = GetComponent<NetworkCharacterControllerPrototype>();
		_forward = transform.forward;
	}

	private void Update()
	{
		if (Object.HasInputAuthority && Input.GetKeyDown(KeyCode.R))
		{
			RPC_SendMessage("Hey Mate!");
		}
	}

	public override void FixedUpdateNetwork() // Fusion에서 시뮬레이션 하는 틱하고 프레임하고 안맞아서 여기서 작업해야 함
	{
		if (GetInput(out NetworkInputData data))
		{
			data.direction.Normalize();
			_cc.Move(5 * data.direction * Runner.DeltaTime);

			if (data.direction.sqrMagnitude > 0)
				_forward = data.direction;

			if (delay.ExpiredOrNotRunning(Runner)) // 생성 빈도를 0.5초로 제한
			{
				if ((data.buttons & NetworkInputData.MOUSEBUTTON1) != 0)
				{
					delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
					Runner.Spawn(_prefabBall,
					  transform.position + _forward,
					  Quaternion.LookRotation(_forward),
					  Object.InputAuthority,
					  (runner, o) =>
					  {
						  // 네트워크에 연결하기 전에 5초 타이머가 굴러가도록 Init
						  o.GetComponent<Ball>().Init();
					  });
					spawned = !spawned;
				}
				else if ((data.buttons & NetworkInputData.MOUSEBUTTON2) != 0)
				{
					delay = TickTimer.CreateFromSeconds(Runner, 0.5f);
					Runner.Spawn(_prefabPhysxBall,
					  transform.position + _forward,
					  Quaternion.LookRotation(_forward),
					  Object.InputAuthority,
					  (runner, o) =>
					  {
						  o.GetComponent<PhysxBall>().Init(10 * _forward);
					  });
					spawned = !spawned;
				}
			}
		}
	}

	public static void OnBallSpawned(Changed<Player> changed)
	{
		changed.Behaviour.material.color = Color.white;
	}

	public override void Render()
	{
		material.color = Color.Lerp(material.color, Color.blue, Time.deltaTime); // 색상의 선형 보간을 처리함.
	}
}
