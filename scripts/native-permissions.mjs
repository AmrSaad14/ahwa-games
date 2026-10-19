// Native project setup: the microphone permission voice chat needs, landscape-only screens, and the
// ahwagames:// link that brings players back to the app after signing in with Google.
// Run after `npx cap add android` / `npx cap add ios` (npm run cap:permissions). Safe to run more than once.
import fs from 'node:fs';

// AdMob app ids (ADMOB_APP_ID_ANDROID / ADMOB_APP_ID_IOS); Google's test app ids until real ones are set
const admobAndroid = process.env.ADMOB_APP_ID_ANDROID || 'ca-app-pub-3940256099942544~3347511713';
const admobIos = process.env.ADMOB_APP_ID_IOS || 'ca-app-pub-3940256099942544~1458002511';

// Android SDK 35: Google Play requires targeting it, and the billing library compiles against it (and needs Android 6.0+)
const vars = 'android/variables.gradle';
if (fs.existsSync(vars)) {
  let g = fs.readFileSync(vars, 'utf8');
  g = g.replace(/compileSdkVersion\s*=\s*\d+/, 'compileSdkVersion = 35').replace(/targetSdkVersion\s*=\s*\d+/, 'targetSdkVersion = 35')
    .replace(/minSdkVersion\s*=\s*\d+/, 'minSdkVersion = 23');   // Android 6.0+: what Google Play Billing needs
  fs.writeFileSync(vars, g);
  const props = 'android/gradle.properties';
  if (fs.existsSync(props) && !fs.readFileSync(props, 'utf8').includes('suppressUnsupportedCompileSdk')) {
    fs.appendFileSync(props, '\n# the Android Gradle plugin that Capacitor 6 uses is fine with SDK 35; this only silences its warning\nandroid.suppressUnsupportedCompileSdk=35\n');
  }
}

const android = 'android/app/src/main/AndroidManifest.xml';
if (fs.existsSync(android)) {
  let xml = fs.readFileSync(android, 'utf8');
  for (const p of ['android.permission.RECORD_AUDIO', 'android.permission.MODIFY_AUDIO_SETTINGS']) {
    if (!xml.includes(p)) xml = xml.replace('<application', `<uses-permission android:name="${p}" />\n    <application`);
  }
  // the games are played sideways: either landscape direction, following the phone's sensor
  if (!xml.includes('android:screenOrientation')) xml = xml.replace(/<activity\b/, '<activity android:screenOrientation="sensorLandscape"');
  // ahwagames://auth?ticket=… opens the game again after Google sign-in in the browser
  if (!xml.includes('android:scheme="ahwagames"')) {
    xml = xml.replace(/(<activity\b[\s\S]*?)(<\/activity>)/, `$1    <intent-filter>
                <action android:name="android.intent.action.VIEW" />
                <category android:name="android.intent.category.DEFAULT" />
                <category android:name="android.intent.category.BROWSABLE" />
                <data android:scheme="ahwagames" android:host="auth" />
            </intent-filter>
        $2`);
  }
  // AdMob needs the app id in the manifest
  if (!xml.includes('com.google.android.gms.ads.APPLICATION_ID')) {
    xml = xml.replace('</application>', `    <meta-data android:name="com.google.android.gms.ads.APPLICATION_ID" android:value="${admobAndroid}" />\n    </application>`);
  }
  fs.writeFileSync(android, xml);
  console.log('Android: microphone permissions added, locked to landscape, ahwagames:// sign-in link, AdMob ' + admobAndroid);
} else console.log('Android project not found (run: npx cap add android)');

// Google Play Games sign-in: a small native plugin (PlayGames) registered in MainActivity, the Games SDK, and the
// game's Play Games app id (PLAY_GAMES_APP_ID, from Play Console → Play Games Services). Without the id the app
// still builds and simply doesn't offer Play Games.
const pgAppId = process.env.PLAY_GAMES_APP_ID || '';
const javaDir = 'android/app/src/main/java/com/ahwa/domino';
if (fs.existsSync(javaDir)) {
  fs.writeFileSync(`${javaDir}/PlayGamesPlugin.java`, `package com.ahwa.domino;

import android.content.pm.ApplicationInfo;
import android.content.pm.PackageManager;
import android.os.Bundle;
import com.getcapacitor.JSObject;
import com.getcapacitor.Plugin;
import com.getcapacitor.PluginCall;
import com.getcapacitor.PluginMethod;
import com.getcapacitor.annotation.CapacitorPlugin;
import com.google.android.gms.games.GamesSignInClient;
import com.google.android.gms.games.PlayGames;
import com.google.android.gms.games.PlayGamesSdk;

/** Signs the player in with Google Play Games and hands the game server a one-time code to check it with Google. */
@CapacitorPlugin(name = "PlayGames")
public class PlayGamesPlugin extends Plugin {
    private boolean ready = false;

    private boolean configured() {
        try {
            ApplicationInfo ai = getContext().getPackageManager().getApplicationInfo(getContext().getPackageName(), PackageManager.GET_META_DATA);
            Bundle md = ai.metaData;
            return md != null && md.get("com.google.android.gms.games.APP_ID") != null;
        } catch (Exception e) { return false; }
    }

    private boolean init() {
        if (ready) return true;
        if (!configured()) return false;
        try { PlayGamesSdk.initialize(getContext()); ready = true; } catch (Exception e) { ready = false; }
        return ready;
    }

    @PluginMethod
    public void available(PluginCall call) {
        JSObject r = new JSObject();
        r.put("available", init());
        call.resolve(r);
    }

    @PluginMethod
    public void signIn(PluginCall call) {
        if (!init()) { call.reject("not_configured"); return; }
        final String clientId = call.getString("clientId", "");
        final boolean interactive = Boolean.TRUE.equals(call.getBoolean("interactive", false));
        final GamesSignInClient client = PlayGames.getGamesSignInClient(getActivity());
        client.isAuthenticated().addOnCompleteListener(t -> {
            boolean ok = t.isSuccessful() && t.getResult() != null && t.getResult().isAuthenticated();
            if (ok) { serverCode(client, clientId, call); return; }
            if (!interactive) { call.reject("not_signed_in"); return; }
            client.signIn().addOnCompleteListener(s -> {
                boolean ok2 = s.isSuccessful() && s.getResult() != null && s.getResult().isAuthenticated();
                if (ok2) serverCode(client, clientId, call); else call.reject("sign_in_failed");
            });
        });
    }

    private void serverCode(GamesSignInClient client, String clientId, PluginCall call) {
        client.requestServerSideAccess(clientId, false).addOnCompleteListener(t -> {
            if (t.isSuccessful() && t.getResult() != null) {
                JSObject r = new JSObject();
                r.put("code", t.getResult());
                call.resolve(r);
            } else call.reject("no_server_code");
        });
    }
}
`);
  fs.writeFileSync(`${javaDir}/MainActivity.java`, `package com.ahwa.domino;

import android.os.Bundle;
import com.getcapacitor.BridgeActivity;

public class MainActivity extends BridgeActivity {
    @Override
    public void onCreate(Bundle savedInstanceState) {
        registerPlugin(PlayGamesPlugin.class);   // Google Play Games sign-in (the plugin lives in this app)
        super.onCreate(savedInstanceState);
    }
}
`);
  const gradle = 'android/app/build.gradle';
  let g = fs.readFileSync(gradle, 'utf8');
  if (!g.includes('play-services-games-v2')) g = g.replace(/dependencies\s*\{/, `dependencies {\n    implementation "com.google.android.gms:play-services-games-v2:19.0.0"`);
  fs.writeFileSync(gradle, g);
  if (pgAppId) {
    const strings = 'android/app/src/main/res/values/strings.xml';
    let sx = fs.readFileSync(strings, 'utf8');
    if (!sx.includes('game_services_project_id')) sx = sx.replace('</resources>', `    <string name="game_services_project_id" translatable="false">${pgAppId}</string>\n</resources>`);
    fs.writeFileSync(strings, sx);
    let mx = fs.readFileSync(android, 'utf8');
    if (!mx.includes('com.google.android.gms.games.APP_ID')) mx = mx.replace('</application>', `    <meta-data android:name="com.google.android.gms.games.APP_ID" android:value="@string/game_services_project_id" />\n    </application>`);
    fs.writeFileSync(android, mx);
  }
  console.log('Android: Play Games sign-in plugin added' + (pgAppId ? ` (app id ${pgAppId})` : ' (no PLAY_GAMES_APP_ID yet: Play Games stays off)'));
}

const ios = 'ios/App/App/Info.plist';
if (fs.existsSync(ios)) {
  let plist = fs.readFileSync(ios, 'utf8');
  if (!plist.includes('NSMicrophoneUsageDescription')) {
    plist = plist.replace(/<dict>/, '<dict>\n\t<key>NSMicrophoneUsageDescription</key>\n\t<string>Talk with the other players at your domino table. يُستخدم الميكروفون للتحدث مع اللاعبين على ترابيزتك.</string>');
  }
  // landscape only, on iPhone and iPad
  const land = '<array>\n\t\t<string>UIInterfaceOrientationLandscapeLeft</string>\n\t\t<string>UIInterfaceOrientationLandscapeRight</string>\n\t</array>';
  plist = plist.replace(/(<key>UISupportedInterfaceOrientations(?:~ipad)?<\/key>\s*)<array>[\s\S]*?<\/array>/g, `$1${land}`);
  // ahwagames:// brings the player back from Google sign-in
  if (!plist.includes('<string>ahwagames</string>')) {
    plist = plist.replace(/<dict>/, '<dict>\n\t<key>CFBundleURLTypes</key>\n\t<array>\n\t\t<dict>\n\t\t\t<key>CFBundleURLName</key>\n\t\t\t<string>com.ahwa.domino.auth</string>\n\t\t\t<key>CFBundleURLSchemes</key>\n\t\t\t<array>\n\t\t\t\t<string>ahwagames</string>\n\t\t\t</array>\n\t\t</dict>\n\t</array>');
  }
  // AdMob: app id, Google's ad network id for SKAdNetwork, and the tracking question shown on iOS 14+
  if (!plist.includes('GADApplicationIdentifier')) {
    plist = plist.replace(/<dict>/, `<dict>\n\t<key>GADApplicationIdentifier</key>\n\t<string>${admobIos}</string>\n\t<key>SKAdNetworkItems</key>\n\t<array>\n\t\t<dict>\n\t\t\t<key>SKAdNetworkIdentifier</key>\n\t\t\t<string>cstr6suwn9.skadnetwork</string>\n\t\t</dict>\n\t</array>\n\t<key>NSUserTrackingUsageDescription</key>\n\t<string>Lets ads in the game be more relevant to you. يساعد في عرض إعلانات أنسب لك.</string>`);
  }
  fs.writeFileSync(ios, plist);
  console.log('iOS: microphone usage description added, locked to landscape, ahwagames:// sign-in link, AdMob ' + admobIos);
} else console.log('iOS project not found (run: npx cap add ios on a Mac)');
