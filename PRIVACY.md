# Privacy

Expeditions Macro runs locally on Windows and has no analytics or telemetry.

It reads pixels from the selected Roblox window or screen region and sends simulated input only while a workflow is running. Camera models, placement models, presets, detector packs, settings, logs, and user-requested diagnostic screenshot ZIPs are stored under `%LocalAppData%\ExpeditionsMacro`.

Manual diagnostic capture is started explicitly from Settings. Users may also opt in to an automatic failure capture that saves 10 Roblox-client screenshots, one per second, after an unexpected Expeditions or Challenge error. Manual Stop does not trigger it. Diagnostic ZIPs contain only captures of the Roblox client plus a local manifest with capture timing and app version. The app does not upload these ZIPs. Review them before sharing because Roblox UI can contain account names, chat, or other personal information.

Discord reporting is optional. When configured, the app sends only the event summary shown in the UI and a Roblox-window screenshot for victory, defeat, or unexpected recovery. Users may also enter a numeric Discord user ID; an unexpected macro failure then sends five separate Components V2 messages that mention only that ID. Manual Stop does not send these alerts. The webhook value is encrypted for the current Windows user with DPAPI and is never written to the application log. The Discord user ID is stored with the other local app settings.

The app may contact GitHub Releases once per day, or when requested, to check for a newer detector pack. Detector updates are downloaded only after the user accepts the update prompt.
