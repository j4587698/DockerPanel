using System;
using System.IO;
using System.Linq;
using System.Text;

namespace scratch_app
{
    class Program
    {
        static void Main(string[] args)
        {
            var sourcePath = @"E:\RiderProjects\DockerPanel\Backend\DockerPanel.API\Services\Acme\CertesAcmeService.cs";
            var lines = File.ReadAllLines(sourcePath);
            
            var providerStart = 93;
            var accountStart = 175;
            var orderStart = 402;
            var challengeStart = 1594;
            var certMgmtStart = 2077;
            var utilsStart = 2465;
            var helpersStart = 2785;
            var endLine = lines.Length;

            WritePartialFile("CertesAcmeService.Providers.cs", lines, providerStart - 1, accountStart - 1);
            WritePartialFile("CertesAcmeService.Accounts.cs", lines, accountStart - 1, orderStart - 1);
            WritePartialFile("CertesAcmeService.Orders.cs", lines, orderStart - 1, challengeStart - 1);
            WritePartialFile("CertesAcmeService.Challenges.cs", lines, challengeStart - 1, certMgmtStart - 1);
            WritePartialFile("CertesAcmeService.Certificates.cs", lines, certMgmtStart - 1, utilsStart - 1);
            WritePartialFile("CertesAcmeService.Utils.cs", lines, utilsStart - 1, helpersStart - 1);
            WritePartialFile("CertesAcmeService.Helpers.cs", lines, helpersStart - 1, endLine - 3);

            var mainContent = new StringBuilder();
            for(int i=0; i<providerStart - 1; i++) // Up to the first region
            {
                var line = lines[i];
                if (line.Contains("public class CertesAcmeService : IAcmeService"))
                {
                    line = line.Replace("public class", "public partial class");
                }
                mainContent.AppendLine(line);
            }
            mainContent.AppendLine("    }");
            mainContent.AppendLine("}");
            
            File.WriteAllText(sourcePath, mainContent.ToString());
            Console.WriteLine("Refactoring completed using partial classes.");
        }

        static void WritePartialFile(string filename, string[] lines, int regionStart, int regionEnd)
        {
            var sb = new StringBuilder();
            // Just output standard usings and the partial class definition
            sb.AppendLine("using System;");
            sb.AppendLine("using System.Collections.Concurrent;");
            sb.AppendLine("using System.Collections.Generic;");
            sb.AppendLine("using System.Linq;");
            sb.AppendLine("using System.Security.Cryptography;");
            sb.AppendLine("using System.Security.Cryptography.X509Certificates;");
            sb.AppendLine("using System.Text;");
            sb.AppendLine("using System.Threading.Tasks;");
            sb.AppendLine("using Certes;");
            sb.AppendLine("using Certes.Acme;");
            sb.AppendLine("using Certes.Acme.Resource;");
            sb.AppendLine("using DockerPanel.API.Models.Acme;");
            sb.AppendLine("using Microsoft.Extensions.Logging;");
            sb.AppendLine("using Microsoft.Extensions.Http;");
            sb.AppendLine("using Microsoft.AspNetCore.SignalR;");
            sb.AppendLine("using DockerPanel.API.Hubs;");
            sb.AppendLine("using DockerPanel.API.Data;");
            sb.AppendLine("using DockerPanel.API.Services.Acme.DnsProviders;");
            sb.AppendLine("using TinyDb;");
            sb.AppendLine("using TinyDb.Bson;");
            sb.AppendLine("using TinyDb.Core;");
            sb.AppendLine("using TinyDb.Collections;");
            sb.AppendLine("using DnsClient;");
            sb.AppendLine("using DockerPanel.API.Services;");
            sb.AppendLine("");
            sb.AppendLine("namespace DockerPanel.API.Services.Acme");
            sb.AppendLine("{");
            sb.AppendLine("    public partial class CertesAcmeService");
            sb.AppendLine("    {");
            
            for(int i=regionStart; i<=regionEnd; i++)
            {
                var line = lines[i];
                if (line.Trim().StartsWith("#region") || line.Trim().StartsWith("#endregion"))
                    continue;
                sb.AppendLine(line);
            }
            
            sb.AppendLine("    }");
            sb.AppendLine("}");
            
            var path = Path.Combine(@"E:\RiderProjects\DockerPanel\Backend\DockerPanel.API\Services\Acme", filename);
            File.WriteAllText(path, sb.ToString());
        }
    }
}
