using System;
using Godot;
using PoingStudios.AdMob.Api;
using PoingStudios.AdMob.Api.Core;
using PoingStudios.AdMob.Api.Listeners;
using PoingStudios.AdMob.Ump.Api;
using PoingStudios.AdMob.Ump.Core;

namespace ConnectFour.Game.Ads;

/// <summary>
/// Real Google AdMob ads on Android, via the Poing Studios AdMob plugin (addons/admob).
/// Startup asks for privacy consent where the law requires it (EU/UK), then initialises the SDK
/// and keeps one interstitial and one rewarded ad loaded, so they show without a wait.
/// If an ad isn't ready (offline, no fill), the game carries on instead of getting stuck.
/// </summary>
public sealed class AdMobAds : IAdService
{
	/// <summary>
	/// Google's public test ad units: they show clearly labelled test ads and never pay out.
	/// Swap in the real unit IDs from the AdMob account before release (and the App ID in
	/// Project Settings, admob/general/android/app_id).
	/// </summary>
	private const string InterstitialUnitId = "ca-app-pub-3940256099942544/1033173712";
	private const string RewardedUnitId = "ca-app-pub-3940256099942544/5224354917";

	private readonly InterstitialAdLoader _interstitialLoader = new();
	private readonly RewardedAdLoader _rewardedLoader = new();
	private InterstitialAd? _interstitial;
	private RewardedAd? _rewarded;
	private bool _ready;

	/// <summary>True when the native AdMob plugin is in this build (Android with the plugin enabled).</summary>
	public static bool Available => Engine.HasSingleton("PoingGodotAdMob");

	public void Start()
	{
		var consent = UserMessagingPlatform.ConsentInformation;
		consent.Update(
			new ConsentRequestParameters(),
			onSuccess: () =>
			{
				bool formNeeded = consent.GetIsConsentFormAvailable()
					&& consent.GetConsentStatus() == ConsentInformation.ConsentStatus.Required;
				if (formNeeded)
					UserMessagingPlatform.LoadConsentForm(form => form.Show(_ => Initialize()), _ => Initialize());
				else
					Initialize();
			},
			onFailure: _ => Initialize());
	}

	private void Initialize()
	{
		if (_ready)
			return;
		// Family-friendly ads only: the game is suitable for children even if not aimed at them.
		MobileAds.SetRequestConfiguration(new RequestConfiguration
		{
			MaxAdContentRating = RequestConfiguration.MaxAdContentRatingG,
		});
		MobileAds.Initialize(new OnInitializationCompleteListener
		{
			OnInitializationComplete = _ =>
			{
				_ready = true;
				DevLog.Write("ad: AdMob initialised");
				LoadInterstitial();
				LoadRewarded();
			},
		});
	}

	public void ShowInterstitial(Action closed)
	{
		var ad = _interstitial;
		if (ad == null)
		{
			DevLog.Write("ad: interstitial not ready, skipped");
			LoadInterstitial();
			closed();
			return;
		}
		_interstitial = null;
		ad.FullScreenContentCallback = new FullScreenContentCallback
		{
			OnAdDismissedFullScreenContent = () => Finish(ad.Destroy, LoadInterstitial, closed),
			OnAdFailedToShowFullScreenContent = _ => Finish(ad.Destroy, LoadInterstitial, closed),
		};
		ad.Show();
	}

	public void ShowRewarded(Action rewarded, Action closed)
	{
		var ad = _rewarded;
		if (ad == null)
		{
			// No ad to show (offline or none available): don't punish the player, grant the undo.
			DevLog.Write("ad: rewarded not ready, granting reward anyway");
			LoadRewarded();
			rewarded();
			closed();
			return;
		}
		_rewarded = null;
		bool earned = false;
		ad.FullScreenContentCallback = new FullScreenContentCallback
		{
			OnAdDismissedFullScreenContent = () =>
			{
				if (earned)
					rewarded();
				Finish(ad.Destroy, LoadRewarded, closed);
			},
			OnAdFailedToShowFullScreenContent = _ =>
			{
				rewarded();
				Finish(ad.Destroy, LoadRewarded, closed);
			},
		};
		ad.Show(new OnUserEarnedRewardListener { OnUserEarnedReward = _ => earned = true });
	}

	private static void Finish(Action destroy, Action reload, Action closed)
	{
		destroy();
		reload();
		closed();
	}

	private void LoadInterstitial()
	{
		if (!_ready)
			return;
		_interstitialLoader.Load(InterstitialUnitId, new AdRequest(), new InterstitialAdLoadCallback
		{
			OnAdLoaded = ad => _interstitial = ad,
			OnAdFailedToLoad = error => DevLog.Write($"ad: interstitial failed to load: {error.Message}"),
		});
	}

	private void LoadRewarded()
	{
		if (!_ready)
			return;
		_rewardedLoader.Load(RewardedUnitId, new AdRequest(), new RewardedAdLoadCallback
		{
			OnAdLoaded = ad => _rewarded = ad,
			OnAdFailedToLoad = error => DevLog.Write($"ad: rewarded failed to load: {error.Message}"),
		});
	}
}
