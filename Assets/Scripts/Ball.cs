using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Fusion;

/// <summary>
/// Description
/// </summary>
public class Ball : NetworkBehaviour
{
	[Networked] // 네트워크에 추가
	private TickTimer life { get; set; }

	public void Init()
	{
		life = TickTimer.CreateFromSeconds(Runner, 5.0f); // 5초 타이머 생성
	}

	public override void FixedUpdateNetwork()
	{
		if (life.Expired(Runner)) Runner.Despawn(Object); // 5초 타이머 만료 후 제거
		else transform.position += 5 * transform.forward * Runner.DeltaTime;
	}
}
