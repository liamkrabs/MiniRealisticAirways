using System.Collections;
using UnityEngine;

namespace MiniRealisticAirways;

public class RunwayClose : Event
{
	public Runway runway_;

	private RendererTint runwayTint_;
	private RendererTint aircraftTint_;
	private RendererTint panelTint_;

	private Aircraft aircraft_;

	private IEnumerator RejectTakeOffCoroutine()
	{
		while (aircraft_ != null && aircraft_.speed > 0f)
		{
			yield return new WaitForFixedUpdate();
		}
		if (aircraft_ == null)
		{
			yield break;
		}
		yield return new WaitForSeconds(1f);
		if (aircraft_ != null)
		{
			aircraft_.ConditionalDestroy();
		}
		aircraft_ = null;
	}

	public override bool Trigger()
	{
		if (runway_ != null || EventManager.closedRunway_ != null)
		{
			return false;
		}
		Aircraft[] outboundAircraft = AircraftManager.GetOutboundAircraft();
		if (outboundAircraft == null)
		{
			return false;
		}
		Aircraft candidate = null;
		foreach (Aircraft aircraft in outboundAircraft)
		{
			if (aircraft != null && aircraft.state == Aircraft.State.TakingOff && aircraft.takeOffRunway != null)
			{
				candidate = aircraft;
				break;
			}
		}
		if (candidate == null)
		{
			return false;
		}
		Runway takeOffRunway = candidate.takeOffRunway;
		if (takeOffRunway == null)
		{
			return false;
		}
		Renderer component = takeOffRunway.Square == null ? null : takeOffRunway.Square.GetComponent<Renderer>();
		if (component == null)
		{
			return false;
		}
		Material material = component.sharedMaterial;
		if (material == null)
		{
			return false;
		}
		aircraft_ = candidate;
		runway_ = takeOffRunway;
		runwayTint_ = new RendererTint(component);
		runwayTint_.Set(new Color(0.7f, 0f, 0f));
		EventManager.closedRunway_ = runway_;
		aircraft_.TakeOffSpeedFactor = 0f;
		aircraft_.targetSpeed = 0f;
		Renderer aircraftRenderer = aircraft_.AP == null ? null : aircraft_.AP.GetComponent<Renderer>();
		Renderer panelRenderer = aircraft_.Panel == null ? null : aircraft_.Panel.GetComponent<Renderer>();
		if (aircraftRenderer != null)
		{
			aircraftTint_ = new RendererTint(aircraftRenderer);
			aircraftTint_.Set(new Color(0.7f, 0f, 0f, 0.3f));
		}
		if (panelRenderer != null)
		{
			panelTint_ = new RendererTint(panelRenderer);
			panelTint_.Set(new Color(0.7f, 0f, 0f, 0.3f));
		}
		StartCoroutine(RejectTakeOffCoroutine());
		Plugin.Log?.LogInfo("RunwayClose Triggered.");
		return true;
	}

	public override void Restore()
	{
		Plugin.Log?.LogWarning("RunwayClose Restored.");
		Runway runway = runway_;
		runway_ = null;
		RestoreTints();
		if (EventManager.closedRunway_ == runway)
		{
			EventManager.closedRunway_ = null;
		}
		aircraft_ = null;
	}

	private void RestoreTints()
	{
		runwayTint_?.Restore();
		aircraftTint_?.Restore();
		panelTint_?.Restore();
		runwayTint_ = aircraftTint_ = panelTint_ = null;
	}

	private void OnDestroy()
	{
		StopAllCoroutines();
		RestoreTints();
		if (EventManager.closedRunway_ == runway_)
		{
			EventManager.closedRunway_ = null;
		}
		runway_ = null;
		aircraft_ = null;
	}
}
