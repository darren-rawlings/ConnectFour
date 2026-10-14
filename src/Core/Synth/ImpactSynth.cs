using System;

namespace ConnectFour.Core.Synth;

/// <summary>
/// Describes how a material rings when struck: a few resonant modes (pitch ratios above the base
/// frequency) that decay exponentially, plus a short burst of filtered noise for the "click".
/// Metal and glass have long decays and inharmonic ratios; wood and stone are short and noisy.
/// </summary>
public sealed record ImpactRecipe(
	double BaseFrequency,
	double[] ModeRatios,
	double[] ModeGains,
	double Decay,
	double NoiseGain,
	double NoiseDecay,
	double NoiseBrightness,
	double Length);

/// <summary>Synthesises sound effects as raw samples so the game needs no licensed audio files.</summary>
public static class ImpactSynth
{
	public const int SampleRate = 44100;

	/// <summary>One impact sound, normalised to a peak of 0.9. Different seeds give natural variations.</summary>
	public static float[] Impact(ImpactRecipe recipe, int seed)
	{
		var rng = new Random(seed);
		int length = (int)(recipe.Length * SampleRate);
		var samples = new float[length];
		double detune = 1.0 + (rng.NextDouble() - 0.5) * 0.12;

		for (int mode = 0; mode < recipe.ModeRatios.Length; mode++)
		{
			double frequency = recipe.BaseFrequency * recipe.ModeRatios[mode] * detune
				* (1.0 + (rng.NextDouble() - 0.5) * 0.03);
			if (frequency > SampleRate * 0.45)
				continue;
			double gain = mode < recipe.ModeGains.Length ? recipe.ModeGains[mode] : 0.0;
			// Higher modes die away faster, which is what makes a strike sound "solid".
			double decay = recipe.Decay / (1.0 + mode * 0.7);
			double phase = rng.NextDouble() * Math.Tau;
			double step = Math.Tau * frequency / SampleRate;
			for (int i = 0; i < length; i++)
			{
				double t = (double)i / SampleRate;
				samples[i] += (float)(gain * Math.Exp(-t / decay) * Math.Sin(phase + step * i));
			}
		}

		double lowPassed = 0.0;
		for (int i = 0; i < length; i++)
		{
			double t = (double)i / SampleRate;
			lowPassed += recipe.NoiseBrightness * (rng.NextDouble() * 2.0 - 1.0 - lowPassed);
			samples[i] += (float)(recipe.NoiseGain * lowPassed * Math.Exp(-t / recipe.NoiseDecay));
		}

		ApplyEdges(samples, attack: 0.0004, release: 0.01);
		Normalize(samples, 0.9f);
		return samples;
	}

	/// <summary>
	/// A seamless loop of band-limited noise with a slight flutter, used for the scrape of a disc
	/// sliding down its slot. Brightness and lowCut (0..1) shape the tone per material.
	/// </summary>
	public static float[] SlideLoop(double brightness, double lowCut, double seconds, int seed)
	{
		var rng = new Random(seed);
		int length = (int)(seconds * SampleRate);
		int fade = SampleRate / 20;
		var raw = new float[length + fade];

		double low = 0.0, rumble = 0.0, flutter = 0.0, flutterTarget = 0.0;
		for (int i = 0; i < raw.Length; i++)
		{
			double white = rng.NextDouble() * 2.0 - 1.0;
			low += brightness * (white - low);
			rumble += lowCut * (low - rumble);
			if (i % (SampleRate / 40) == 0)
				flutterTarget = rng.NextDouble();
			flutter += 0.002 * (flutterTarget - flutter);
			raw[i] = (float)((low - rumble) * (0.65 + 0.35 * flutter));
		}

		// Crossfade the overshoot back into the start so the loop point is inaudible.
		var loop = new float[length];
		Array.Copy(raw, loop, length);
		for (int i = 0; i < fade; i++)
		{
			float mix = (float)i / fade;
			loop[i] = raw[i] * mix + raw[length + i] * (1f - mix);
		}

		Normalize(loop, 0.8f);
		return loop;
	}

	/// <summary>Converts samples in [-1, 1] to little-endian signed 16-bit PCM.</summary>
	public static byte[] ToPcm16(float[] samples)
	{
		var bytes = new byte[samples.Length * 2];
		for (int i = 0; i < samples.Length; i++)
		{
			short value = (short)Math.Round(Math.Clamp(samples[i], -1f, 1f) * short.MaxValue);
			bytes[i * 2] = (byte)(value & 0xFF);
			bytes[i * 2 + 1] = (byte)((value >> 8) & 0xFF);
		}
		return bytes;
	}

	private static void ApplyEdges(float[] samples, double attack, double release)
	{
		int attackSamples = Math.Max(1, (int)(attack * SampleRate));
		int releaseSamples = Math.Max(1, (int)(release * SampleRate));
		for (int i = 0; i < attackSamples && i < samples.Length; i++)
			samples[i] *= (float)i / attackSamples;
		for (int i = 0; i < releaseSamples && i < samples.Length; i++)
			samples[samples.Length - 1 - i] *= (float)i / releaseSamples;
	}

	private static void Normalize(float[] samples, float peak)
	{
		float max = 0f;
		foreach (float s in samples)
			max = Math.Max(max, Math.Abs(s));
		if (max <= 0f)
			return;
		float scale = peak / max;
		for (int i = 0; i < samples.Length; i++)
			samples[i] *= scale;
	}
}
