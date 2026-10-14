// Adds the microphone permission voice chat needs to the native projects.
// Run after `npx cap add android` / `npx cap add ios` (npm run cap:permissions). Safe to run more than once.
import fs from 'node:fs';

const android = 'android/app/src/main/AndroidManifest.xml';
if (fs.existsSync(android)) {
  let xml = fs.readFileSync(android, 'utf8');
  for (const p of ['android.permission.RECORD_AUDIO', 'android.permission.MODIFY_AUDIO_SETTINGS']) {
    if (!xml.includes(p)) xml = xml.replace('<application', `<uses-permission android:name="${p}" />\n    <application`);
  }
  fs.writeFileSync(android, xml);
  console.log('Android: microphone permissions added');
} else console.log('Android project not found (run: npx cap add android)');

const ios = 'ios/App/App/Info.plist';
if (fs.existsSync(ios)) {
  let plist = fs.readFileSync(ios, 'utf8');
  if (!plist.includes('NSMicrophoneUsageDescription')) {
    plist = plist.replace(/<dict>/, '<dict>\n\t<key>NSMicrophoneUsageDescription</key>\n\t<string>Talk with the other players at your domino table. يُستخدم الميكروفون للتحدث مع اللاعبين على ترابيزتك.</string>');
  }
  fs.writeFileSync(ios, plist);
  console.log('iOS: microphone usage description added');
} else console.log('iOS project not found (run: npx cap add ios on a Mac)');
