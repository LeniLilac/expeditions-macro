# Privacy

Expeditions Macro runs locally on Windows and has no analytics or telemetry.

It reads pixels from the selected Roblox window or screen region and sends simulated input only while a workflow is running. Camera models, placement models, presets, detector packs, settings, logs, and user-requested diagnostic screenshot ZIPs are stored under `%LocalAppData%\ExpeditionsMacro`.

Diagnostic capture is started explicitly from Settings. Its ZIP contains only captures of the Roblox client plus a local manifest with capture timing and app version. The app does not upload the ZIP. Review it before sharing because Roblox UI can contain account names, chat, or other personal information.

Discord reporting is optional. When configured, the app sends only the event summary shown in the UI and a Roblox-window screenshot for victory, defeat, or unexpected recovery. The webhook value is encrypted for the current Windows user with DPAPI. It is never written to the application log.

The app may contact GitHub Releases once per day, or when requested, to check for a newer detector pack. Detector updates are downloaded only after the user accepts the update prompt.
