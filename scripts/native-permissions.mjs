// Native project setup: the microphone permission voice chat needs, landscape-only screens, and the
// ahwagames:// link that brings players back to the app after signing in with Google.
// Run after `npx cap add android` / `npx cap add ios` (npm run cap:permissions). Safe to run more than once.
import fs from 'node:fs';

// AdMob app ids (ADMOB_APP_ID_ANDROID / ADMOB_APP_ID_IOS); Google's test app ids until real ones are set
const admobAndroid = process.env.ADMOB_APP_ID_ANDROID || 'ca-app-pub-3940256099942544~3347511713';
const admobIos = process.env.ADMOB_APP_ID_IOS || 'ca-app-pub-3940256099942544~1458002511';

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
