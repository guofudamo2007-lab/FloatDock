# Media capsule (v0.2)

User reference: https://www.douyin.com/video/7687863850850356495 — AmorFate's AF Media Bar. The video page identifies https://github.com/Fervent-Tempo/AF-Media-Bar. Inspected the video frame showing the compact taskbar player and expanded cover/transport/timeline card; the upstream README describes synchronized lyrics and source switching.

## Integration

Add an optional media capsule inside FloatDock between app icons and right-side utilities. Default on; settings can disable it. In v0.3 the empty state collapses to a 36 DIP music button; an active source expands to 190 DIP with artwork, title/artist or the current lyric line. Hover reveals previous/play/next; click opens a floating card with source selector, artwork, full title, transport, seek slider and synchronized lyrics. Scrolling over the capsule cycles sources. It follows the dock's fullscreen hiding and reduced-motion preference.

Use Windows GSMTC via the Windows SDK projection. Unsupported controls are disabled; commands target the selected session and report refusal. Metadata and session changes invalidate stale thumbnail/lyrics responses. Closing or disabling the widget disposes timers and requests. No global media-key simulation and no Explorer injection.

Lyrics: load a user-selected local LRC file, or opt into LRCLIB automatic matching. The opt-in text explains that title, artist, album and duration are sent to lrclib.net. Default is offline. Support line-synced LRC, multi-timestamps and offset; enhanced LRC word timestamps highlight words when provided. Standard line-only lyrics highlight the active line without claiming word timing. No lyric text is logged or committed. Song/session changes clear local lyric state; a failed lookup falls back to title/artist.

## Validation

Tests cover LRC parsing, offsets, seek/pause synchronization, command routing and rejection, stale async results, disabled controls, repeated lyric transitions, WPF layout/render and lifecycle. The opt-in `--media-integration` test publishes an owned SMTC fixture with `GetForWindow`, checks real GSMTC discovery/metadata/stable identity, and receives real pause and seek events. It plays no audio and only commands the exact unique test-title session, then clears and disables it. The default CI suite does not depend on the desktop media service. Do not claim third-party playback or human animation acceptance from these tests. Audio routing, per-app volume and real spectrum capture remain separate features; no animated fake spectrum is presented as audio data.

The GSMTC/lyrics/media UI implementation remains original. v0.3 adds an explicit-start native taskbar host derived from AF Media Bar / FluentFlyout, with AF free-space and visibility policies; source reuse and GPL provenance are recorded in THIRD-PARTY-NOTICES.md. The same capsule is reused in either native or standalone mode. Current native hosting has only source review and offline validation; no desktop acceptance is claimed. Live media integration checks now require both --desktop and --media-integration. Public references: Microsoft Windows.Media.Control documentation and https://lrclib.net/docs.
