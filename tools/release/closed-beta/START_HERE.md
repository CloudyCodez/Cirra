# Cirra v0.4.3 Closed Beta

Thank you for helping test Cirra. This build is a portable Windows x64 beta: it does not install itself, and it keeps its library data under your local Cirra profile.

## Start Here

1. Extract the entire ZIP to a normal folder such as `Documents\Cirra Beta`. Do not run it from inside the ZIP.
2. Open the `app` folder and run `Cirra.exe`.
3. Begin with the short checks in `TESTING_GUIDE.md`.

This update fixes Steam discovery for custom client locations. Fresh releases such as Castlevania: Belmont's Curse are now read directly from Steam's authoritative install manifest even when Steam itself is installed outside Program Files.

This build creates a local indexed library beside the portable JSON library. The JSON file remains the source of truth and can still be backed up or moved normally. The index is rebuilt automatically and should not be shared in bug reports unless the test coordinator specifically asks for it.

## A Few Important Notes

- Windows may show a SmartScreen warning because this is an unsigned beta. Only run it if you received the ZIP and checksum directly from the Cirra test coordinator.
- This is test software. Keep a backup of anything important before changing emulator settings, attaching manuals, or testing save backup and restore.
- Do not redistribute the package or screenshots containing someone else's private game paths or profile data.
- To stop testing, simply close Cirra. Deleting the extracted folder removes this portable copy, but not its local app data.

Please send feedback with the game title/platform, exact steps, what you expected, what happened, and a screenshot or short video when useful.
