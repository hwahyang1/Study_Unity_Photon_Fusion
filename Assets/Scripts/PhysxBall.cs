using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Fusion;

/// <summary>
/// Description
/// </summary>
public class PhysxBall : NetworkBehaviour
{
	[Networked]
	private TickTimer life { get; set; }

	public void Init(Vector3 forward)
	{
		life = TickTimer.CreateFromSeconds(Runner, 5.0f);
		GetComponent<Rigidbody>().velocity = forward; // Ball 스크립트와 달리 얘의 물리연산은 클라이언트 재량임
	}

	public override void FixedUpdateNetwork()
	{
		if (life.Expired(Runner)) Runner.Despawn(Object);
	}
}
