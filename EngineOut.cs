using System.Collections;
using DG.Tweening;
using TMPro;
using UnityEngine;

namespace MiniRealisticAirways;

public class EngineOut : Event
{
	private string callSign_ = "CA3115";

	private const float RANDOM_BASE_TIME = 15f;

	private const float RANDOM_TIME_OFFSET_LIMIT = 5f;

	public override bool Trigger()
	{
		Aircraft aircraft = null;
		Aircraft[] outboundAircraft = AircraftManager.GetOutboundAircraft();
		if (outboundAircraft == null)
		{
			return false;
		}
		foreach (Aircraft aircraft2 in outboundAircraft)
		{
			if (AircraftState.GetAircraftStates(aircraft2, out var _, out var _, out var aircraftType) && aircraft2.state == Aircraft.State.TakingOff && aircraftType.weight_ == Weight.Medium)
			{
				aircraft = aircraft2;
				break;
			}
		}
		if (aircraft == null)
		{
			return false;
		}
		StartCoroutine(EngineOutCoroutine(aircraft));
		Plugin.Log?.LogInfo("EngineOut Triggered.");
		return true;
	}

	private IEnumerator EngineOutCoroutine(Aircraft aircraft)
	{
		InitCallSign();
		while (aircraft != null && aircraft.state == Aircraft.State.TakingOff)
		{
			yield return new WaitForSeconds(1f);
		}
		if (aircraft == null)
		{
			yield break;
		}
		yield return new WaitForSeconds(RANDOM_BASE_TIME + 2f * RANDOM_TIME_OFFSET_LIMIT * Random.value - RANDOM_TIME_OFFSET_LIMIT);
		AircraftAltitude aircraftAltitude = null;
		AircraftSpeed aircraftSpeed = null;
		AircraftType aircraftType = null;
		while (aircraft != null && !AircraftState.GetAircraftStates(aircraft, out aircraftAltitude, out aircraftSpeed, out aircraftType))
		{
			yield return new WaitForFixedUpdate();
		}
		if (aircraft == null)
		{
			yield break;
		}
		aircraftAltitude.tcasAction_ = TCASAction.Disabled;
		aircraftAltitude.altitudeDisabled_ = true;
		aircraftSpeed.speedDisabled_ = true;
		ShowCallSign(aircraft);
		yield return TextDisplayCoroutine(aircraft, ModLocalization.Get("engine.mayday"), 3f, 2f);
		yield return TextDisplayCoroutine(aircraft, ModLocalization.Format("engine.failure", callSign_), 5f, 3f);
		yield return TextDisplayCoroutine(aircraft, ModLocalization.Get("engine.return"), 5f, 3f);
		if (aircraft == null || aircraft.AP == null || AircraftManager.Instance == null)
		{
			yield break;
		}
		CreateReturnFlight(aircraft, aircraftAltitude, aircraftSpeed, aircraftType);
	}

	private void CreateReturnFlight(Aircraft original, AircraftAltitude altitude, AircraftSpeed speed, AircraftType type)
	{
		// 先成功创建替代机，再销毁原机；创建失败不会丢失原机。
		Aircraft returning = AircraftManager.Instance.CreateInboundAircraft(original.AP.transform.position, original.heading);
		if (returning == null)
		{
			altitude.altitudeDisabled_ = false;
			altitude.tcasAction_ = TCASAction.None;
			speed.speedDisabled_ = false;
			Plugin.Log?.LogWarning("Engine-out return aircraft creation failed; original aircraft retained.");
			return;
		}
		ReturnFlightState initial = returning.gameObject.AddComponent<ReturnFlightState>();
		initial.Weight = type.weight_;
		initial.Altitude = altitude.altitude_;
		initial.TargetAltitude = altitude.targetAltitude_;
		initial.Speed = original.speed;
		initial.TargetSpeed = original.targetSpeed;
		initial.CallSign = callSign_;
		original.ConditionalDestroy();
	}

	private void ShowCallSign(Aircraft aircraft)
	{
		if (aircraft != null && aircraft.callsignText != null)
		{
			aircraft.ShowCallSign(show: true);
			aircraft.callsignText.text = callSign_;
		}
	}

	private IEnumerator TextDisplayCoroutine(Aircraft aircraft, string text, float dialogueLength, float disappearTime)
	{
		if (aircraft == null)
		{
			yield break;
		}
		GameObject obj = new GameObject("Text");
		obj.transform.SetParent(aircraft.transform);
		obj.transform.localPosition = new Vector3(0f, 2f, -9f);
		TMP_Text dialogue = obj.AddComponent<TextMeshPro>();
		dialogue.fontSize = 4f;
		dialogue.horizontalAlignment = HorizontalAlignmentOptions.Center;
		dialogue.verticalAlignment = VerticalAlignmentOptions.Top;
		dialogue.rectTransform.sizeDelta = new Vector2(10f, 1f);
		dialogue.gameObject.SetActive(value: true);
		// Resolve the font from the text's complete localized payload once, then
		// type it out without repeating font discovery on every character.
		ChineseTypography.SetHudText(dialogue, text, "Engine-out HUD");
		ArabicTextFormatter.SetText(dialogue, "");
		dialogue.color = Color.white;
		float timePassed = 0f;
		if (ModLocalization.IsRtl)
		{
			// 阿拉伯字母连写形态依赖上下文，逐字揭示会让已显示字符逐帧变形；
			// 按词揭示，只有正在追加的词会重新定型。
			string[] words = text.Split(' ');
			int revealedWords = 0;
			while (aircraft != null && dialogue != null && timePassed < dialogueLength)
			{
				int target = Mathf.Min(words.Length, (int)((float)words.Length * (timePassed / dialogueLength)) + 1);
				if (target != revealedWords)
				{
					revealedWords = target;
					ArabicTextFormatter.SetText(dialogue, string.Join(" ", words, 0, revealedWords));
				}
				timePassed += Time.unscaledDeltaTime * 1.75f;
				yield return null;
			}
		}
		else
		{
			int[] elements = System.Globalization.StringInfo.ParseCombiningCharacters(text);
			int revealed = -1;
			while (aircraft != null && dialogue != null && timePassed < dialogueLength)
			{
				int count = Mathf.Min(elements.Length, (int)(elements.Length * (timePassed / dialogueLength)));
				if (count != revealed)
				{
					revealed = count;
					int end = count == elements.Length ? text.Length : elements[count];
					ArabicTextFormatter.SetText(dialogue, text.Substring(0, end));
				}
				timePassed += Time.unscaledDeltaTime * 1.75f;
				yield return null;
			}
		}
		if (dialogue == null)
		{
			yield break;
		}
		ArabicTextFormatter.SetText(dialogue, text);
		yield return new WaitForSeconds(disappearTime);
		if (dialogue == null || aircraft == null) yield break;
		Tween fade = dialogue.DOFade(0f, 1f).SetUpdate(isIndependentUpdate: true);
		try
		{
			yield return new WaitForSeconds(1f);
		}
		finally
		{
			fade.Kill();
			if (dialogue != null) Object.Destroy(dialogue.gameObject);
		}
	}

	private void InitCallSign()
	{
		callSign_ = "CA" + Random.Range(1000, 9999);
	}
}
