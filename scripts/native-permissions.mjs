// Native project setup: the microphone permission voice chat needs, and landscape-only screens.
// Run after `npx cap add android` / `npx cap add ios` (npm run cap:permissions). Safe to run more than once.
import fs from 'node:fs';

const android = 'android/app/src/main/AndroidManifest.xml';
if (fs.existsSync(android)) {
  let xml = fs.readFileSync(android, 'utf8');
  for (const p of ['android.permission.RECORD_AUDIO', 'android.permission.MODIFY_AUDIO_SETTINGS']) {
    if (!xml.includes(p)) xml = xml.replace('<application', `<uses-permission android:name="${p}" />\n    <application`);
  }
  // the games are played sideways: either landscape direction, following the phone's sensor
  if (!xml.includes('android:screenOrientation')) xml = xml.replace(/<activity\b/, '<activity android:screenOrientation="sensorLandscape"');
  fs.writeFileSync(android, xml);
  console.log('Android: microphone permissions added, locked to landscape');
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
  fs.writeFileSync(ios, plist);
  console.log('iOS: microphone usage description added, locked to landscape');
} else console.log('iOS project not found (run: npx cap add ios on a Mac)');
