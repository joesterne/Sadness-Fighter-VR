using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.Build;
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
        const string PasswordVariable="AFTERHOURS_KEYTOOL_PASSWORD";
        public static bool Available=>File.Exists(Keystore)&&File.Exists(PasswordFile);

        [MenuItem("After Hours/Create release signing key",false,1600)]
        public static void Create()
        {
            if(Available){Debug.Log("[ReleaseSigning] The release signing files already exist.");return;}
            if(File.Exists(Keystore)||File.Exists(PasswordFile))
                throw new BuildFailedException("The release signing files are incomplete. Restore the matching file from your private backup.");
            string keytool=FindKeytool();
            if(keytool==null){Debug.LogError("[ReleaseSigning] Could not find keytool. Install Android Build Support (with OpenJDK) for this Unity version.");return;}
            var bytes=new byte[18];using(var rng=RandomNumberGenerator.Create())rng.GetBytes(bytes);
            string password=Convert.ToBase64String(bytes).Replace('+','A').Replace('/','b').TrimEnd('=');
            string staging=Path.Combine("UserSettings","signing-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(staging);
            string stagedKey=Path.Combine(staging,Path.GetFileName(Keystore));
            string stagedPassword=Path.Combine(staging,Path.GetFileName(PasswordFile));
            try
            {
                string args="-genkeypair -keystore \""+Path.GetFullPath(stagedKey)+"\" -storetype PKCS12"+
                            " -storepass:env "+PasswordVariable+" -keypass:env "+PasswordVariable+" -alias "+Alias+
                            " -keyalg RSA -keysize 2048 -validity 10000 -dname \"CN=After Hours, O=After Hours Studio\"";
                var info=new ProcessStartInfo(keytool,args){UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true};
                info.EnvironmentVariables[PasswordVariable]=password;
                using(var process=Process.Start(info))
                {
                    process.StandardInput.Close();
                    if(!process.WaitForExit(60000))
                    {
                        process.Kill();
                        process.WaitForExit();
                        throw new BuildFailedException("Release key generation timed out.");
                    }
                    if(process.ExitCode!=0)throw new BuildFailedException("keytool failed to generate the release key (exit "+process.ExitCode+").");
                }
                File.WriteAllText(stagedPassword,password);
                // File.Move refuses to overwrite. Concurrent requests cannot publish files from different pairs.
                File.Move(stagedPassword,PasswordFile);
                try{File.Move(stagedKey,Keystore);}
                catch{File.Delete(PasswordFile);throw;}
            }
            finally{Directory.Delete(staging,true);}
            Debug.Log("[ReleaseSigning] Created the release key at "+Path.GetFullPath(Keystore)+". Back up it and "+Path.GetFileName(PasswordFile)+" somewhere safe and private: without them you cannot publish updates to the same app.");
        }

        internal static string ReadPassword()
        {
            if(!Available)throw new BuildFailedException("Release signing requires both files in UserSettings. Restore the pair from backup, or use After Hours → Create release signing key for a new app.");
            string password=File.ReadAllText(PasswordFile).Trim();
            if(password.Length==0)throw new BuildFailedException("The release signing password file is empty. Restore it from your private backup.");
            return password;
        }

        internal static void Apply(string password)
        {
            PlayerSettings.Android.keystoreName=Path.GetFullPath(Keystore);
            PlayerSettings.Android.keystorePass=password;PlayerSettings.Android.keyaliasName=Alias;PlayerSettings.Android.keyaliasPass=password;
            PlayerSettings.Android.useCustomKeystore=true;
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
