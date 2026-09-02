// Windows-only diagnostic plug-in for Orbital Rift.
// It captures the default render device through WASAPI loopback and reduces samples to five
// instantaneous visualizer values. It does not write, upload, or retain raw audio.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <mmdeviceapi.h>
#include <audioclient.h>
#include <cmath>
#include <cstdint>
#include <cstring>

#pragma comment(lib, "Ole32.lib")

namespace
{
    IAudioClient* gClient = nullptr;
    IAudioCaptureClient* gCapture = nullptr;
    WAVEFORMATEX* gFormat = nullptr;
    bool gComInitialized = false;
    bool gCapturing = false;
    float gEnergy = 0.f;
    float gBass = 0.f;
    float gMid = 0.f;
    float gTreble = 0.f;
    float gBeat = 0.f;
    float gPreviousEnergy = 0.f;
    float gLowState = 0.f;
    float gMidState = 0.f;

    float Clamp01(float value)
    {
        return value < 0.f ? 0.f : (value > 1.f ? 1.f : value);
    }

    void ReleaseLoopback()
    {
        if (gClient) gClient->Stop();
        if (gCapture) { gCapture->Release(); gCapture = nullptr; }
        if (gClient) { gClient->Release(); gClient = nullptr; }
        if (gFormat) { CoTaskMemFree(gFormat); gFormat = nullptr; }
        if (gComInitialized) { CoUninitialize(); gComInitialized = false; }
        gCapturing = false;
        gEnergy = gBass = gMid = gTreble = gBeat = gPreviousEnergy = 0.f;
        gLowState = gMidState = 0.f;
    }

    float ReadSample(const BYTE* data, int bytesPerSample)
    {
        if (bytesPerSample == 4)
        {
            float value = 0.f;
            memcpy(&value, data, sizeof(float));
            return value;
        }
        if (bytesPerSample == 2)
        {
            int16_t value = 0;
            memcpy(&value, data, sizeof(int16_t));
            return value / 32768.f;
        }
        if (bytesPerSample == 3)
        {
            int32_t value = data[0] | (data[1] << 8) | (data[2] << 16);
            if (value & 0x00800000) value |= 0xFF000000;
            return value / 8388608.f;
        }
        return 0.f;
    }

    void Decay()
    {
        gEnergy *= .88f;
        gBass *= .88f;
        gMid *= .88f;
        gTreble *= .88f;
        gBeat *= .58f;
        gPreviousEnergy = gEnergy;
    }

    void Analyze(const BYTE* data, UINT32 frames, DWORD flags)
    {
        if (!data || (flags & AUDCLNT_BUFFERFLAGS_SILENT) || frames == 0) { Decay(); return; }

        const int channels = gFormat->nChannels > 0 ? gFormat->nChannels : 1;
        const int bytesPerSample = gFormat->wBitsPerSample / 8;
        if (bytesPerSample < 2 || bytesPerSample > 4) { Decay(); return; }

        double energy = 0.0;
        double bass = 0.0;
        double mid = 0.0;
        double treble = 0.0;
        const BYTE* cursor = data;
        for (UINT32 frame = 0; frame < frames; ++frame)
        {
            float mono = 0.f;
            for (int channel = 0; channel < channels; ++channel)
            {
                mono += ReadSample(cursor, bytesPerSample);
                cursor += bytesPerSample;
            }
            mono /= static_cast<float>(channels);
            gLowState += .040f * (mono - gLowState);
            gMidState += .220f * (mono - gMidState);
            const float low = gLowState;
            const float middle = gMidState - gLowState;
            const float high = mono - gMidState;
            energy += mono * mono;
            bass += low * low;
            mid += middle * middle;
            treble += high * high;
        }

        const float inverseFrames = 1.f / static_cast<float>(frames);
        const float nextEnergy = Clamp01(static_cast<float>(std::sqrt(energy * inverseFrames)) * 7.5f);
        const float nextBass = Clamp01(static_cast<float>(std::sqrt(bass * inverseFrames)) * 12.f);
        const float nextMid = Clamp01(static_cast<float>(std::sqrt(mid * inverseFrames)) * 13.f);
        const float nextTreble = Clamp01(static_cast<float>(std::sqrt(treble * inverseFrames)) * 14.f);
        const float onset = Clamp01((nextEnergy - gPreviousEnergy * 1.10f) * 4.f);
        gPreviousEnergy = nextEnergy;
        gEnergy = gEnergy * .55f + nextEnergy * .45f;
        gBass = gBass * .55f + nextBass * .45f;
        gMid = gMid * .55f + nextMid * .45f;
        gTreble = gTreble * .55f + nextTreble * .45f;
        gBeat = gBeat * .46f + onset * .54f;
    }
}

extern "C"
{
    __declspec(dllexport) int OR_StartAudioLoopback()
    {
        if (gCapturing) return 1;

        const HRESULT comResult = CoInitializeEx(nullptr, COINIT_MULTITHREADED);
        if (SUCCEEDED(comResult)) gComInitialized = true;

        IMMDeviceEnumerator* enumerator = nullptr;
        IMMDevice* device = nullptr;
        HRESULT result = CoCreateInstance(__uuidof(MMDeviceEnumerator), nullptr, CLSCTX_ALL,
            __uuidof(IMMDeviceEnumerator), reinterpret_cast<void**>(&enumerator));
        if (SUCCEEDED(result)) result = enumerator->GetDefaultAudioEndpoint(eRender, eConsole, &device);
        if (SUCCEEDED(result)) result = device->Activate(__uuidof(IAudioClient), CLSCTX_ALL, nullptr, reinterpret_cast<void**>(&gClient));
        if (SUCCEEDED(result)) result = gClient->GetMixFormat(&gFormat);
        if (SUCCEEDED(result)) result = gClient->Initialize(AUDCLNT_SHAREMODE_SHARED, AUDCLNT_STREAMFLAGS_LOOPBACK, 0, 0, gFormat, nullptr);
        if (SUCCEEDED(result)) result = gClient->GetService(__uuidof(IAudioCaptureClient), reinterpret_cast<void**>(&gCapture));
        if (SUCCEEDED(result)) result = gClient->Start();
        if (device) device->Release();
        if (enumerator) enumerator->Release();
        if (FAILED(result)) { ReleaseLoopback(); return 0; }
        gCapturing = true;
        return 1;
    }

    __declspec(dllexport) void OR_StopAudioLoopback()
    {
        ReleaseLoopback();
    }

    __declspec(dllexport) int OR_PollAudioFrame(float* energy, float* bass, float* mid, float* treble, float* beat)
    {
        if (!gCapturing || !gCapture) return 0;
        UINT32 packets = 0;
        HRESULT result = gCapture->GetNextPacketSize(&packets);
        while (SUCCEEDED(result) && packets > 0)
        {
            BYTE* data = nullptr;
            UINT32 frames = 0;
            DWORD flags = 0;
            result = gCapture->GetBuffer(&data, &frames, &flags, nullptr, nullptr);
            if (FAILED(result)) break;
            Analyze(data, frames, flags);
            gCapture->ReleaseBuffer(frames);
            result = gCapture->GetNextPacketSize(&packets);
        }
        if (FAILED(result)) { ReleaseLoopback(); return 0; }
        if (packets == 0) Decay();
        if (energy) *energy = gEnergy;
        if (bass) *bass = gBass;
        if (mid) *mid = gMid;
        if (treble) *treble = gTreble;
        if (beat) *beat = gBeat;
        return 1;
    }
}
