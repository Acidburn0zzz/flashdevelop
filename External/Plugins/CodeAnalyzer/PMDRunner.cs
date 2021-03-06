﻿using System;
using System.IO;
using System.Timers;
using System.Windows.Forms;
using System.Xml;
using PluginCore;
using PluginCore.Localization;
using PluginCore.Managers;
using PluginCore.Utilities;
using Timer = System.Timers.Timer;

namespace CodeAnalyzer
{
    public class PMDRunner
    {
        string errorLog;
        string watchedFile;
        ProcessRunner pmdRunner;
        FileSystemWatcher pmdWatcher;
        Timer deleteTimer;

        /// <summary>
        /// Runs the pmd analyzer process
        /// </summary>
        public static void Analyze(string pmdPath, string projectPath, string sourcePath, string pmdRuleset)
        {
            try
            {
                var pr = new PMDRunner();
                var objDir = Path.Combine(projectPath, "obj");
                if (!Directory.Exists(objDir)) Directory.CreateDirectory(objDir);
                pr.RunPMD(pmdPath, objDir, sourcePath, pmdRuleset);
                pr.WatchFile(objDir);
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
            }
        }

        /// <summary>
        /// Start background process
        /// </summary>
        void RunPMD(string pmdPath, string projectPath, string sourcePath, string pmdRuleset)
        {
            string args = "-Xmx256m -jar \"" + pmdPath + "\" -s \"" + sourcePath + "\" -o \"" + projectPath + "\"";
            if (File.Exists(pmdRuleset)) args += " -r \"" + pmdRuleset + "\"";
            SetStatusText(TextHelper.GetString("Info.RunningFlexPMD"));
            pmdRunner = new ProcessRunner();
            pmdRunner.ProcessEnded += PmdRunnerProcessEnded;
            pmdRunner.Error += PmdRunnerError;
            pmdRunner.Run("java", args, true);
            errorLog = string.Empty;
        }

        /// <summary>
        /// Trace process done message to the output panel
        /// </summary>
        void PmdRunnerProcessEnded(object sender, int exitCode)
        {
            if (exitCode != 0)
            {
                pmdWatcher.EnableRaisingEvents = false;
                PluginBase.MainForm.CallCommand("PluginCommand", "ResultsPanel.ClearResults");
                TraceManager.Add(errorLog);
            }
        }

        /// <summary>
        /// Log output so that we can show it on error
        /// </summary>
        void PmdRunnerError(object sender, string line) => errorLog += line + "\n";

        /// <summary>
        /// Watched the spcified file for creation
        /// </summary>
        void WatchFile(string projectPath)
        {
            pmdWatcher = new FileSystemWatcher();
            pmdWatcher.EnableRaisingEvents = false;
            pmdWatcher.Filter = "pmd.xml";
            pmdWatcher.Created += onCreateFile;
            deleteTimer = new Timer();
            deleteTimer.Enabled = false;
            deleteTimer.AutoReset = false;
            deleteTimer.Interval = 500;
            deleteTimer.Elapsed += onTimedDelete;
            watchedFile = Path.Combine(projectPath, "pmd.xml");
            string oldFile = Path.ChangeExtension(watchedFile, "old");
            if (File.Exists(watchedFile))
            {
                if (File.Exists(oldFile)) File.Delete(oldFile);
                File.Move(watchedFile, oldFile);
            }
            pmdWatcher.Path = projectPath;
            pmdWatcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Stops the timer after file creation
        /// </summary>
        void onCreateFile(object source, FileSystemEventArgs e)
        {
            if (e.Name.ToLower() == "pmd.xml")
            {
                pmdWatcher.EnableRaisingEvents = false;
                deleteTimer.Enabled = true;
            }
        }

        /// <summary>
        /// Deletes the generated file after read
        /// </summary>
        void onTimedDelete(object sender, ElapsedEventArgs e)
        {
            var mainForm = (Form) PluginBase.MainForm;
            if (mainForm.InvokeRequired)
            {
                mainForm.BeginInvoke((MethodInvoker)delegate { onTimedDelete(sender, e); });
                return;
            }
            try
            {
                if (File.Exists(watchedFile))
                {
                    string currFile = string.Empty;
                    PluginBase.MainForm.CallCommand("PluginCommand", "ResultsPanel.ClearResults");
                    XmlTextReader reader = new XmlTextReader(watchedFile);
                    while (reader.Read())
                    {
                        if (reader.NodeType == XmlNodeType.Element && reader.Name == "file")
                        {
                            currFile = reader.GetAttribute("name");
                        }
                        else if (reader.NodeType == XmlNodeType.Element && reader.Name == "violation")
                        {
                            int state = 0;
                            int line = Convert.ToInt32(reader.GetAttribute("beginline"));
                            int col = Convert.ToInt32(reader.GetAttribute("begincolumn"));
                            line = line > 0 ? line : 0; col = col > 0 ? col : 0;
                            switch (reader.GetAttribute("priority"))
                            {
                                case "1": state = 3; break;
                                case "3": state = 2; break;
                                case "5": state = 0; break;
                            }
                            string item =
                                $"{currFile}:{line}: col: {col}: {reader.ReadElementContentAsString().Trim()}";
                            TraceManager.Add(item, state);
                        }
                    }
                    reader.Close();
                    string oldFile = Path.ChangeExtension(watchedFile, "old");
                    if (File.Exists(watchedFile))
                    {
                        if (File.Exists(oldFile)) File.Delete(oldFile);
                        File.Move(watchedFile, oldFile);
                    }
                    PluginBase.MainForm.CallCommand("PluginCommand", "ResultsPanel.ShowResults");
                    SetStatusText(TextHelper.GetString("Info.FlexPMDFinished"));
                }
            }
            catch (Exception ex)
            {
                ErrorManager.ShowError(ex);
            }
        }

        /// <summary>
        /// Sets the status text
        /// </summary>
        public void SetStatusText(string text)
        {
            PluginBase.MainForm.StatusStrip.Items[0].Text = "  " + text;
        }
    }
}