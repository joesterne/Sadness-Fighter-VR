using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace AfterHours.Editor
{
    // The Meta Developer Dashboard only accepts APKs signed with your own release key, never Unity's debug key.
    // This creates that key once and applies it only while a release APK builds. The key file stays in UserSettings (which git ignores);
    // its password lives in .env at the project root (also ignored), as AFTERHOURS_KEY_PASSWORD. AFTERHOURS_KEYSTORE can point at a key
    // kept somewhere else. A password left in the older UserSettings/AfterHours-release-key.txt is moved into .env the first time it is used.
    // Back up the key and .env: an app on the Meta store can only ever be updated with the key it was first uploaded with.
    public static class ReleaseSigning
    {
        public const string DefaultKeystore="UserSettings/AfterHours-release.keystore",LegacyPasswordFile="UserSettings/AfterHours-release-key.txt";
        public const string PasswordVariable="AFTERHOURS_KEY_PASSWORD",KeystoreVariable="AFTERHOURS_KEYSTORE";
        const string Alias="afterhours";
        public static string Keystore=>DotEnv.Get(KeystoreVariable)??DefaultKeystore;
        public static bool Available=>File.Exists(Keystore)&&Password()!=null;

        static string Password()
        {
            var password=DotEnv.Get(PasswordVariable);
            if(password!=null)return password;
            if(!File.Exists(LegacyPasswordFile))return null;
            password=File.ReadAllText(LegacyPasswordFile).Trim();if(password.Length==0)return null;
            DotEnv.Set(PasswordVariable,password);
            Debug.Log("[ReleaseSigning] Moved the release key password into .env ("+PasswordVariable+"). Once .env is backed up with the key, "+LegacyPasswordFile+" can be deleted.");
            return password;
        }

        [MenuItem("After Hours/Create release signing key",false,1600)]
        public static void Create()
        {
            if(Available){Debug.Log("[ReleaseSigning] A release key already exists at "+Path.GetFullPath(Keystore)+". It is never replaced: the Meta store only accepts updates signed with the same key.");return;}
            string keytool=FindKeytool();
            if(keytool==null){Debug.LogError("[ReleaseSigning] Could not find keytool. Install Android Build Support (with OpenJDK) for this Unity version.");return;}
            var bytes=new byte[18];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
            string password=Convert.ToBase64String(bytes).Replace('+','A').Replace('/','b').TrimEnd('=');
            string keystore=Keystore;var folder=Path.GetDirectoryName(Path.GetFullPath(keystore));if(!string.IsNullOrEmpty(folder))Directory.CreateDirectory(folder);
            string args="-genkeypair -keystore \""+Path.GetFullPath(keystore)+"\" -storetype PKCS12 -storepass:env "+PasswordVariable+" -keypass:env "+PasswordVariable+" -alias "+Alias+
                        " -keyalg RSA -keysize 2048 -validity 10000 -dname \"CN=After Hours, O=After Hours Studio\"";
            var info=new ProcessStartInfo(keytool,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            // The password reaches keytool through its environment, not its command line, which other programs on the PC can read.
            info.EnvironmentVariables[PasswordVariable]=password;
            using var p=Process.Start(info);string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();
            if(!p.WaitForExit(60000)){try{p.Kill();}catch{}Debug.LogError("[ReleaseSigning] keytool did not finish within a minute, so no key was created.");return;}
            if(p.ExitCode!=0||!File.Exists(keystore)){Debug.LogError("[ReleaseSigning] keytool failed:\n"+output);return;}
            DotEnv.Set(PasswordVariable,password);
            Debug.Log("[ReleaseSigning] Created the release key at "+Path.GetFullPath(keystore)+" and saved its password in .env. Back up both somewhere safe and private: without them you cannot publish updates to the same app.");
        }

        public static bool Apply()
        {
            string password=Password();
            if(password==null||!File.Exists(Keystore))return false;
            PlayerSettings.Android.useCustomKeystore=true;PlayerSettings.Android.keystoreName=Path.GetFullPath(Keystore);
            PlayerSettings.Android.keystorePass=password;PlayerSettings.Android.keyaliasName=Alias;PlayerSettings.Android.keyaliasPass=password;
            return true;
        }
        // Afterwards the project goes back to the debug key, so no path or password stays in the project settings.
        public static void Clear()
        {
            PlayerSettings.Android.useCustomKeystore=false;PlayerSettings.Android.keystoreName="";
            PlayerSettings.Android.keystorePass="";PlayerSettings.Android.keyaliasName="";PlayerSettings.Android.keyaliasPass="";
        }

        static string FindKeytool()
        {
            string exe=Application.platform==RuntimePlatform.WindowsEditor?"keytool.exe":"keytool";
            string configured=null;
            try
            {
                var settings=Type.GetType("UnityEditor.Android.AndroidExternalToolsSettings, UnityEditor.Android.Extensions");
                configured=settings?.GetProperty("jdkRootPath",BindingFlags.Public|BindingFlags.Static)?.GetValue(null) as string;
            }
            catch(Exception){}
            foreach(var root in new[]{configured,Path.Combine(EditorApplication.applicationContentsPath,"PlaybackEngines","AndroidPlayer","OpenJDK")})
            {
                if(string.IsNullOrEmpty(root))continue;string candidate=Path.Combine(root,"bin",exe);
                if(File.Exists(candidate))return candidate;
            }
            return null;
        }
    }
}
