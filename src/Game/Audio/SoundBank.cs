using System.Collections.Generic;
using ConnectFour.Core.Synth;
using Godot;

namespace ConnectFour.Game.Audio;

/// <summary>Synthesises each material's sounds the first time they're needed, then reuses them.</summary>
public static class SoundBank
{
	private const int ImpactVariations = 4;

	private static readonly Dictionary<Style, AudioStreamWav[]> Impacts = new();
	private static readonly Dictionary<Style, AudioStreamWav> Slides = new();

	public static AudioStreamWav RandomImpact(Style style)
	{
		if (!Impacts.TryGetValue(style, out var clips))
		{
			var recipe = Styles.Impact(style);
			clips = new AudioStreamWav[ImpactVariations];
			for (int i = 0; i < clips.Length; i++)
				clips[i] = ToStream(ImpactSynth.Impact(recipe, seed: (int)style * 100 + i), loop: false);
			Impacts[style] = clips;
		}
		return clips[GD.RandRange(0, clips.Length - 1)];
	}

	public static AudioStreamWav Slide(Style style)
	{
		if (!Slides.TryGetValue(style, out var clip))
		{
			var (brightness, lowCut) = Styles.Slide(style);
			clip = ToStream(ImpactSynth.SlideLoop(brightness, lowCut, seconds: 1.0, seed: (int)style), loop: true);
			Slides[style] = clip;
		}
		return clip;
	}

	private static AudioStreamWav ToStream(float[] samples, bool loop)
	{
		var stream = new AudioStreamWav
		{
			Format = AudioStreamWav.FormatEnum.Format16Bits,
			MixRate = ImpactSynth.SampleRate,
			Stereo = false,
			Data = ImpactSynth.ToPcm16(samples),
		};
		if (loop)
		{
			stream.LoopMode = AudioStreamWav.LoopModeEnum.Forward;
			stream.LoopBegin = 0;
			stream.LoopEnd = samples.Length;
		}
		return stream;
	}
}
