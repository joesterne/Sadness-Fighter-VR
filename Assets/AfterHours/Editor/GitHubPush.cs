using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using UnityEditor;
using UnityEngine;
using Debug=UnityEngine.Debug;

namespace AfterHours.Editor
{
    // Commits every change git doesn't ignore and pushes the current branch to GitHub: git add, commit and push, as in a
    // terminal. Large files (the APK, textures, native plugins) go through Git LFS. Git runs on a background thread so
    // the editor stays responsive, and the first push may open Git Credential Manager's GitHub sign-in window.
    // Each step is written to the Console and to Logs/AfterHours-git-push.txt.
    public class GitHubPush : EditorWindow
    {
        const string MessageFile="Temp/AfterHours-commit-message.txt",IdentityFile="Temp/AfterHours-git-identity.txt",LogFile="Logs/AfterHours-git-push.txt";
        const string NameKey="AfterHours.GitName",EmailKey="AfterHours.GitEmail";
        // Files that hold this computer's network address, access tokens or session ids. A push stops if any is staged.
        static readonly string[] NeverCommit={"Assets/Resources/DevAgentSettings.asset","Assets/#Meta/session_id.id"};
        static volatile bool running;static volatile string status="";
        static readonly object sync=new object();static readonly StringBuilder log=new StringBuilder();
        string message="",authorName="",authorEmail="";Vector2 messageScroll,logScroll;

        [MenuItem("After Hours/Commit and push to GitHub…",false,3000)]
        static void Open(){var w=GetWindow<GitHubPush>(true,"Push to GitHub");w.minSize=new Vector2(620,460);w.LoadMessage();w.Show();}
        void LoadMessage()
        {
            message=File.Exists(MessageFile)?File.ReadAllText(MessageFile).Replace("\r\n","\n").Trim():"Update After Hours";
            authorName=EditorPrefs.GetString(NameKey,"");authorEmail=EditorPrefs.GetString(EmailKey,"");
            if(authorEmail.Length==0&&File.Exists(IdentityFile)){var lines=File.ReadAllLines(IdentityFile);if(lines.Length>=2){authorName=lines[0].Trim();authorEmail=lines[1].Trim();}}
        }
        void OnInspectorUpdate(){Repaint();}
        void OnGUI()
        {
            EditorGUILayout.LabelField("Commits every change that git doesn't ignore, then pushes the current branch to GitHub. The release APK is included through Git LFS.",EditorStyles.wordWrappedLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commit message",EditorStyles.boldLabel);
            messageScroll=EditorGUILayout.BeginScrollView(messageScroll,GUILayout.Height(170));
            using(new EditorGUI.DisabledScope(running))message=EditorGUILayout.TextArea(message,GUILayout.ExpandHeight(true));
            EditorGUILayout.EndScrollView();
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Commit as. Saved in this repository's git settings. If GitHub keeps your email private, use your GitHub noreply address.",EditorStyles.wordWrappedMiniLabel);
            using(new EditorGUI.DisabledScope(running))
            {
                authorName=EditorGUILayout.TextField("Name",authorName);
                authorEmail=EditorGUILayout.TextField("Email",authorEmail);
            }
            using(new EditorGUI.DisabledScope(running||string.IsNullOrWhiteSpace(message)))
                if(GUILayout.Button(running?"Working…":"Commit and push",GUILayout.Height(32)))
                {
                    EditorPrefs.SetString(NameKey,authorName.Trim());EditorPrefs.SetString(EmailKey,authorEmail.Trim());
                    Begin(message,authorName.Trim(),authorEmail.Trim());
                }
            EditorGUILayout.LabelField(status,EditorStyles.wordWrappedLabel);
            string text;lock(sync)text=log.ToString();
            logScroll=EditorGUILayout.BeginScrollView(logScroll,GUILayout.ExpandHeight(true));
            EditorGUILayout.SelectableLabel(text,EditorStyles.wordWrappedMiniLabel,GUILayout.ExpandHeight(true),GUILayout.MinHeight(140));
            EditorGUILayout.EndScrollView();
        }

        public static void Begin(string text,string name="",string email="")
        {
            if(running)return;
            AssetDatabase.SaveAssets();
            for(int i=0;i<UnityEngine.SceneManagement.SceneManager.sceneCount;i++)
                if(UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty){status="Save or discard the open scene's changes first.";Debug.LogWarning("[GitHubPush] "+status);return;}
            running=true;status="Starting…";lock(sync)log.Clear();
            string root=Directory.GetParent(Application.dataPath).FullName;
            new Thread(()=>Run(root,text,name,email)){IsBackground=true,Name="After Hours git push"}.Start();
        }

        static void Run(string root,string text,string name,string email)
        {
            try
            {
                Step("Checking the repository");
                string branch=Git(root,"rev-parse --abbrev-ref HEAD").Trim();
                string remote=Git(root,"remote get-url origin").Trim();
                string configured=Git(root,"config user.email",check:false).Trim();
                if(name.Length>0&&email.Length>0&&!string.Equals(email,configured,StringComparison.OrdinalIgnoreCase))
                {
                    if(name.Contains("\"")||email.Contains("\""))throw new Exception("The name and email can't contain quotation marks.");
                    // Stored in this repository's own settings, not globally.
                    Git(root,"config user.name \""+name+"\"");Git(root,"config user.email \""+email+"\"");configured=email;
                    Note("Commits from this repository are now made as "+name+" <"+email+">.");
                }
                if(configured.Length==0)throw new Exception("Git doesn't know who you are yet. Fill in Name and Email under 'Commit as', then push again.");
                Note("Branch "+branch+" → "+remote);
                Step("Checking GitHub for newer work");
                Git(root,"fetch origin",600);
                bool remoteBranch=Code(root,"rev-parse --verify --quiet origin/"+branch)==0;
                // Never overwrite work on GitHub that isn't on this computer.
                if(remoteBranch&&Code(root,"merge-base --is-ancestor origin/"+branch+" HEAD")!=0)
                    throw new Exception("GitHub has commits on "+branch+" that this computer doesn't. Pull them first, then push again.");
                // A commit made under an earlier identity that never reached GitHub takes the current one.
                if(remoteBranch)
                {
                    var authors=Git(root,"log origin/"+branch+"..HEAD --format=%ae").Split('\n').Select(a=>a.Trim()).Where(a=>a.Length>0).ToList();
                    if(authors.Any(a=>!string.Equals(a,configured,StringComparison.OrdinalIgnoreCase)))
                    {
                        if(authors.Count>1)throw new Exception("Unpushed commits were made as a different author. Fix them with an interactive rebase, then push again.");
                        Step("Updating the unpushed commit's author to "+configured);
                        Git(root,"commit --amend --no-edit --reset-author -q",600);
                    }
                }
                Step("Staging changes");
                Git(root,"add -A",900);
                var staged=Git(root,"diff --cached --name-only").Split('\n').Select(s=>s.Trim()).Where(s=>s.Length>0).ToList();
                var blocked=staged.Where(s=>NeverCommit.Any(n=>string.Equals(s,n,StringComparison.OrdinalIgnoreCase))).ToList();
                if(blocked.Count>0){Git(root,"reset -q");throw new Exception("Stopped before committing "+string.Join(", ",blocked)+". It holds this computer's address, an access token or a session id. Add it to .gitignore.");}
                if(staged.Count>0)
                {
                    Note(staged.Count+" changed files.");
                    string messagePath=Path.Combine(root,"Temp","AfterHours-commit-message.git.txt");
                    File.WriteAllText(messagePath,text.Replace("\r\n","\n").Trim()+"\n",new UTF8Encoding(false));
                    Step("Committing");
                    Note(Git(root,"commit -q -F \""+messagePath+"\"",600));
                }
                else Note("Nothing new to commit.");
                if(remoteBranch&&Git(root,"rev-list --count origin/"+branch+"..HEAD").Trim()=="0"){Finish("GitHub is already up to date ("+branch+").",false);return;}
                Step("Pushing to GitHub. Large files go through Git LFS, so this can take several minutes");
                var (pushCode,pushOutput)=Execute(root,"push -u origin "+branch,3600);
                Note(pushOutput);
                if(pushCode!=0&&pushOutput.Contains("GH007"))
                    throw new Exception("GitHub keeps your email address private, so it refused commits that show it. Under 'Commit as', use your GitHub noreply address (GitHub → Settings → Emails), then push again. Nothing was lost; the commit is kept here.");
                if(pushCode!=0)throw new Exception("git push failed ("+pushCode+"). See the log above.");
                Finish("Pushed "+Git(root,"rev-parse --short HEAD").Trim()+" to "+remote+" ("+branch+").",false);
            }
            catch(Exception e){Finish(e.Message,true);}
            finally{running=false;}
        }

        static void Step(string text){status=text+"…";Append("== "+text);Debug.Log("[GitHubPush] "+text);}
        static void Note(string text){text=text.Trim();if(text.Length>0)Append(text);}
        static void Finish(string text,bool failed)
        {
            status=text;Append((failed?"FAILED: ":"DONE: ")+text);
            if(failed)Debug.LogError("[GitHubPush] "+text);else Debug.Log("[GitHubPush] "+text);
        }
        static void Append(string text)
        {
            lock(sync)
            {
                log.AppendLine(text);
                try{Directory.CreateDirectory("Logs");File.WriteAllText(LogFile,DateTime.UtcNow.ToString("u")+"\n"+log);}catch(Exception){}
            }
        }

        static string GitExe()
        {
            foreach(var candidate in new[]{@"C:\Program Files\Git\cmd\git.exe",@"C:\Program Files (x86)\Git\cmd\git.exe"})if(File.Exists(candidate))return candidate;
            return "git";
        }
        static int Code(string root,string args)=>Execute(root,args,120).code;
        static string Git(string root,string args,int timeoutSeconds=120,bool check=true)
        {
            var (code,output)=Execute(root,args,timeoutSeconds);
            if(check&&code!=0)throw new Exception("git "+args+" failed ("+code+"):\n"+output.Trim());
            return output;
        }
        static (int code,string output) Execute(string root,string args,int timeoutSeconds)
        {
            var info=new ProcessStartInfo(GitExe(),args){WorkingDirectory=root,UseShellExecute=false,RedirectStandardOutput=true,RedirectStandardError=true,RedirectStandardInput=true,CreateNoWindow=true};
            using var p=Process.Start(info);
            // No terminal is attached: any text prompt sees end of input and fails rather than waiting forever.
            p.StandardInput.Close();
            var stdout=p.StandardOutput.ReadToEndAsync();var stderr=p.StandardError.ReadToEndAsync();
            if(!p.WaitForExit(timeoutSeconds*1000)){try{p.Kill();}catch(Exception){}return (-1,"git "+args+" timed out after "+timeoutSeconds+" s");}
            p.WaitForExit();
            return (p.ExitCode,stdout.Result+stderr.Result);
        }
    }
}
