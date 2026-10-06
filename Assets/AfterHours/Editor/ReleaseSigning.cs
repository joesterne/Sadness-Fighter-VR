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
    // This creates that key once, keeps it in UserSettings (which git ignores) and applies it only while a release APK builds.
    // Back up both files: an app on the Meta store can only ever be updated with the key it was first uploaded with.
    public static class ReleaseSigning
    {
        public const string Keystore="UserSettings/AfterHours-release.keystore",PasswordFile="UserSettings/AfterHours-release-key.txt";
        const string Alias="afterhours";
        public static bool Available=>File.Exists(Keystore)&&File.Exists(PasswordFile);

        [MenuItem("After Hours/Create release signing key",false,1600)]
        public static void Create()
        {
            if(Available){Debug.Log("[ReleaseSigning] A release key already exists at "+Path.GetFullPath(Keystore)+". It is never replaced: the Meta store only accepts updates signed with the same key.");return;}
            string keytool=FindKeytool();
            if(keytool==null){Debug.LogError("[ReleaseSigning] Could not find keytool. Install Android Build Support (with OpenJDK) for this Unity version.");return;}
            var bytes=new byte[18];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
            string password=Convert.ToBase64String(bytes).Replace('+','A').Replace('/','b').TrimEnd('=');
            Directory.CreateDirectory("UserSettings");
            string args="-genkeypair -keystore \""+Path.GetFullPath(Keystore)+"\" -storetype PKCS12 -storepass "+password+" -keypass "+password+" -alias "+Alias+
                        " -keyalg RSA -keysize 2048 -validity 10000 -dname \"CN=After Hours, O=After Hours Studio\"";
            var info=new ProcessStartInfo(keytool,args){UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,CreateNoWindow=true};
            using var p=Process.Start(info);string output=p.StandardOutput.ReadToEnd()+p.StandardError.ReadToEnd();p.WaitForExit(60000);
            if(p.ExitCode!=0||!File.Exists(Keystore)){Debug.LogError("[ReleaseSigning] keytool failed:\n"+output);return;}
            File.WriteAllText(PasswordFile,password);
            Debug.Log("[ReleaseSigning] Created the release key at "+Path.GetFullPath(Keystore)+". Back up it and "+Path.GetFileName(PasswordFile)+" somewhere safe and private: without them you cannot publish updates to the same app.");
        }

        public static bool Apply()
        {
            if(!Available)return false;
            string password=File.ReadAllText(PasswordFile).Trim();
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
