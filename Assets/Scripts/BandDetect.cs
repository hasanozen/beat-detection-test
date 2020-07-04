using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BandDetect : MonoBehaviour
{
    AudioSource audio;
    public static float[] samples = new float[512];
    public static float[] frequencyBands = new float[8];
    public static float[] bandBuffer = new float[8];
    float[] bufferDecrease = new float[8];
    private void Start()
    {
        audio = GetComponent<AudioSource>();
    }
    
    void Update()
    {
        GetSpectrumAudioSource();
        MakeFreqBands();
        BandBuffer();
    }
    private void GetSpectrumAudioSource()
    {
        audio.GetSpectrumData(samples, 0, FFTWindow.Blackman);
    }

    private void BandBuffer()
    {
        for (int g = 0; g < frequencyBands.Length; g++)
        {
            if (frequencyBands[g] > bandBuffer[g])
            {
                bandBuffer[g] = frequencyBands[g];
                bufferDecrease[g] = .005f;
            }

            if (frequencyBands[g] < bandBuffer[g])
            {
                bandBuffer[g] -= bufferDecrease[g];
                bufferDecrease[g] *= 1.2f;
            }
        }
    }    

    private void MakeFreqBands()
    {
        /* Seperates all frequencies to
         * specified bands with taking
         * frequencies's averages
         */

        int count = 0;
        for (int i = 0; i < frequencyBands.Length; i++)
        {
            float average = 0;
            int sampleCount = (int)Math.Pow(2, i) * 2;

            if (i == 7)
                count += 2;

            for (int j = 0; j < sampleCount; j++)
            {
                average += samples[count] * (count + 1);
                count++;
            }

            average /= count;
            frequencyBands[i] = average * 10;
        }
    }

    
}
