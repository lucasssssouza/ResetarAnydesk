using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ResetarAnydesk
{
    public partial class Menu : Form
    {
        // Onde armazenar o backup (pasta do usuário)
        private readonly string _backupFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "ResetarAnydesk",
            "backupAnydesk"
        );

        public Menu()
        {
            InitializeComponent();
            // Executa backup no load, sem bloquear UI
            Task.Run(() => Backup());
        }

        private async void btnResetar_Click(object sender, EventArgs e)
        {
            btnResetar.Enabled = false;
            try
            {
                // 1) Garante anydesk.exe em Downloads
                string downloadsFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads"
                );
                string anydeskPath = Path.Combine(downloadsFolder, "anydesk.exe");

                if (!File.Exists(anydeskPath))
                    await DownloadAnyDesk(anydeskPath);

                // 2) Workflow de reset em thread de fundo
                await Task.Run(() => ResetWorkflow(anydeskPath));

                MessageBox.Show(
                    "AnyDesk resetado com sucesso!",
                    "Sucesso",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                );
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Erro: {ex.Message}",
                    "Falha",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
            finally
            {
                btnResetar.Enabled = true;
            }
        }

        private async Task DownloadAnyDesk(string targetPath)
        {
            const string url = "https://download.anydesk.com/AnyDesk.exe";
            using (var client = new WebClient())
            {
                await client.DownloadFileTaskAsync(new Uri(url), targetPath);
            }

            // Mensagem na UI
            Invoke((Action)(() =>
                MessageBox.Show(
                    "AnyDesk baixado na pasta de Downloads.",
                    "Download concluído",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information
                )
            ));
        }

        private void ResetWorkflow(string anydeskPath)
        {
            // 2.1) Backup + encerra
            try
            {
                Backup();
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Falha no backup/encerramento", ex);
            }

            // 2.2) Desinstalação silenciosa
            try
            {
                var uninstallString = GetUninstallString("anydesk");
                if (!string.IsNullOrEmpty(uninstallString))
                {
                    RunUninstall(uninstallString);
                    Thread.Sleep(2000);
                }
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Falha na desinstalação silenciosa", ex);
            }

            // 2.3) Limpeza de pastas antigas
            try
            {
                RemoveDirectoryTree(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "AnyDesk"
                    )
                );
                RemoveDirectoryTree(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
                        "AnyDesk"
                    )
                );
                RemoveDirectoryTree(
                    Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                        "AnyDesk"
                    )
                );
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Falha ao limpar pastas antigas", ex);
            }

            // 2.4) Restore (inicia → espera → encerra → restaura → reinicia)
            try
            {
                Restore(anydeskPath);
            }
            catch (Exception ex)
            {
                throw new ApplicationException("Falha no processo de restauração", ex);
            }

            // 2.5) Limpa backup
            try
            {
                if (Directory.Exists(_backupFolder))
                    Directory.Delete(_backupFolder, true);
            }
            catch
            {
                // ignora
            }
        }

        private void Backup()
        {
            // Mata o AnyDesk, se estiver rodando
            foreach (var proc in Process.GetProcessesByName("AnyDesk"))
            {
                using (proc)
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
            }

            // Caminho original do user.conf
            string userConf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnyDesk",
                "user.conf"
            );

            // Recria pasta de backup
            if (Directory.Exists(_backupFolder))
                Directory.Delete(_backupFolder, true);
            Directory.CreateDirectory(_backupFolder);

            // Copia o arquivo
            string dest = Path.Combine(_backupFolder, "user.conf");
            if (File.Exists(userConf))
                File.Copy(userConf, dest, overwrite: true);
        }

        private void Restore(string anydeskExePath)
        {
            // 1) Inicia AnyDesk limpo
            using (var pStart = Process.Start(anydeskExePath))
            {
                // 2) Aguarda criação da pasta de config
                string cfgDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "AnyDesk"
                );
                int maxTries = 40; // até ~20s
                int tries = 0;
                while (!Directory.Exists(cfgDir) && tries++ < maxTries)
                    Thread.Sleep(500);
            }

            // 3) Encerra todas as instâncias
            foreach (var proc in Process.GetProcessesByName("AnyDesk"))
            {
                using (proc)
                {
                    proc.Kill();
                    proc.WaitForExit(5000);
                }
            }

            // 4) Restaura o user.conf do backup
            string userConf = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "AnyDesk",
                "user.conf"
            );
            string src = Path.Combine(_backupFolder, "user.conf");
            if (File.Exists(src))
                File.Copy(src, userConf, overwrite: true);

            // 5) Reinicia o AnyDesk já com configuração restaurada
            Process.Start(anydeskExePath)?.Dispose();
        }

        // -----------------------
        // Helpers
        // -----------------------

        private string GetUninstallString(string displayNamePart)
        {
            string[] roots = {
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall",
                @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall"
            };

            foreach (var root in roots)
            {
                using (var key = Registry.LocalMachine.OpenSubKey(root))
                {
                    if (key == null) continue;
                    foreach (var sub in key.GetSubKeyNames())
                    {
                        using (var sk = key.OpenSubKey(sub))
                        {
                            var dn = sk?.GetValue("DisplayName") as string;
                            if (!string.IsNullOrEmpty(dn) &&
                                dn.IndexOf(displayNamePart, StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                return sk.GetValue("UninstallString") as string;
                            }
                        }
                    }
                }
            }

            return null;
        }

        private void RunUninstall(string uninstallString)
        {
            string exe = uninstallString;
            string args = "/SILENT /VERYSILENT /SUPPRESSMSGBOXES";

            if (exe.StartsWith("\""))
            {
                int idx = uninstallString.IndexOf('\"', 1);
                exe = uninstallString.Substring(1, idx - 1);
                var extra = uninstallString.Substring(idx + 1).Trim();
                if (extra.Length > 0)
                    args = extra + " " + args;
            }
            else if (uninstallString.Contains(".exe "))
            {
                int i = uninstallString.IndexOf(".exe ") + 4;
                exe = uninstallString.Substring(0, i).Trim();
                var extra = uninstallString.Substring(i).Trim();
                if (extra.Length > 0)
                    args = extra + " " + args;
            }

            var psi = new ProcessStartInfo(exe, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true
            };
            using (var p = Process.Start(psi))
            {
                p?.WaitForExit();
            }
        }

        private void RemoveDirectoryTree(string dir)
        {
            if (!Directory.Exists(dir)) return;
            foreach (var f in Directory.GetFiles(dir, "*", SearchOption.AllDirectories))
                File.SetAttributes(f, FileAttributes.Normal);
            Directory.Delete(dir, true);
        }
    }
}
