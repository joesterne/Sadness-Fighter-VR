using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AfterHours.Editor
{
    // Local secrets live in .env at the project root (next to Assets/). Git ignores it and the repository is public, so nothing in it is
    // ever pushed. .env.example lists the names with placeholder values. A real environment variable with the same name wins over the file,
    // so a build machine can supply secrets without a file at all.
    public static class DotEnv
    {
        public const string File=".env";

        public static string Get(string name)
        {
            var fromEnvironment=Environment.GetEnvironmentVariable(name);
            if(!string.IsNullOrEmpty(fromEnvironment))return fromEnvironment;
            return Read().TryGetValue(name,out var value)&&value.Length>0?value:null;
        }

        // Reads KEY=value lines. Blank lines and # comments are skipped, "export " is allowed, and matching quotes round a value are removed.
        public static Dictionary<string,string> Read()
        {
            var values=new Dictionary<string,string>(StringComparer.Ordinal);
            if(!System.IO.File.Exists(File))return values;
            foreach(var raw in System.IO.File.ReadAllLines(File))
            {
                var line=raw.Trim();
                if(line.Length==0||line.StartsWith("#"))continue;
                if(line.StartsWith("export "))line=line.Substring(7).TrimStart();
                int eq=line.IndexOf('=');if(eq<=0)continue;
                string key=line.Substring(0,eq).Trim(),value=line.Substring(eq+1).Trim();
                if(value.Length>=2&&(value[0]=='"'||value[0]=='\'')&&value[value.Length-1]==value[0])value=value.Substring(1,value.Length-2);
                values[key]=value;
            }
            return values;
        }

        // Sets one value, keeping every other line (and its comments) as it was.
        public static void Set(string name,string value)
        {
            var lines=System.IO.File.Exists(File)?System.IO.File.ReadAllLines(File).ToList():new List<string>{"# After Hours local secrets. Never commit this file (it is in .gitignore). See .env.example."};
            string entry=name+"="+value;bool found=false;
            for(int i=0;i<lines.Count;i++)
            {
                var t=lines[i].TrimStart();if(t.StartsWith("export "))t=t.Substring(7).TrimStart();
                if(t.StartsWith(name+"=",StringComparison.Ordinal)){lines[i]=entry;found=true;}
            }
            if(!found)lines.Add(entry);
            System.IO.File.WriteAllText(File,string.Join("\n",lines)+"\n");
        }
    }
}
