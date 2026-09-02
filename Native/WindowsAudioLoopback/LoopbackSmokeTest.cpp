// Local diagnostic executable; not shipped with the game.
#define WIN32_LEAN_AND_MEAN
#include <windows.h>
#include <cstdio>

using Start = int (*)();
using Stop = void (*)();
using Poll = int (*)(float*, float*, float*, float*, float*);

int main()
{
    HMODULE module = LoadLibraryA("OrbitalRiftAudioLoopback.dll");
    if (!module) { std::puts("Could not load OrbitalRiftAudioLoopback.dll"); return 2; }
    const auto start = reinterpret_cast<Start>(GetProcAddress(module, "OR_StartAudioLoopback"));
    const auto stop = reinterpret_cast<Stop>(GetProcAddress(module, "OR_StopAudioLoopback"));
    const auto poll = reinterpret_cast<Poll>(GetProcAddress(module, "OR_PollAudioFrame"));
    if (!start || !stop || !poll || !start()) { std::puts("Could not start loopback"); FreeLibrary(module); return 3; }

    for (int i = 0; i < 20; ++i)
    {
        float energy = 0.f, bass = 0.f, mid = 0.f, treble = 0.f, beat = 0.f;
        poll(&energy, &bass, &mid, &treble, &beat);
        std::printf("%02d energy=%.3f bass=%.3f mid=%.3f treble=%.3f beat=%.3f\n", i, energy, bass, mid, treble, beat);
        Sleep(100);
    }
    stop();
    FreeLibrary(module);
    return 0;
}
