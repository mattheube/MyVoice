# Discord for MyVoice

Public Application ID: `1558080529360887909`.

## Rich Presence

The desktop application talks to the installed Discord client over local IPC. No OAuth secret or bot token is needed. Presence is opt-in in Settings → Account & Privacy, and sends only `MyVoice · Studio audio`. It does not publish voice names, microphone audio, filenames, account identity or private creations. Disable the setting to clear the activity. Discord must be running and allow activity display.

## Sign-in — remaining owner configuration

1. In [Discord OAuth2](https://discord.com/developers/applications/1558080529360887909/oauth2), add this redirect URL:
   `https://smcjototzljmyontxekz.supabase.co/auth/v1/callback`
2. In [Supabase Auth providers](https://supabase.com/dashboard/project/smcjototzljmyontxekz/auth/providers), enable Discord. Set the Client ID above and paste the Discord Client Secret **directly into Supabase**. Never put this secret in source control, the desktop application, the website or a chat. No bot permissions are needed.
3. In [Supabase URL configuration](https://supabase.com/dashboard/project/smcjototzljmyontxekz/auth/url-configuration), retain `https://mattheube.github.io/MyVoice/` and add the desktop loopback redirect allowlist entry:
   `http://127.0.0.1:*/myvoice-discord/*`
4. Save and verify the public `/auth/v1/settings` response reports `external.discord: true`.

Desktop sign-in opens the user's browser and waits on a random loopback port with a random callback path. Both clients use S256 PKCE; no Discord client secret ships to users. The desktop stores the resulting MyVoice session under Windows DPAPI. The website stores it only for the current browser tab. Code/verifier values are single-use, time-limited and never logged. Browser denial, cancellation, provider-disabled and expired-callback paths are supported.

Before declaring production login validated, complete a real consent flow on both the installed desktop build and deployed site, test denial, then sign out and restart. Local simulated provider tests do not replace this check.

References: [Supabase Discord sign-in](https://supabase.com/docs/guides/auth/social-login/auth-discord), [PKCE](https://supabase.com/docs/guides/auth/sessions/pkce-flow), [Discord RPC](https://discord.com/developers/topics/rpc).
