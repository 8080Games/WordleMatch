"""Shared helpers for the daily Wordle scripts."""

import subprocess
import sys


def utf8_stdio():
    """Under Task Scheduler the console defaults to cp1252; force UTF-8 so emoji don't crash prints."""
    for stream in (sys.stdout, sys.stderr):
        try:
            stream.reconfigure(encoding='utf-8', errors='replace')
        except (AttributeError, ValueError):
            pass


def toast(title, message):
    """Show a Windows toast notification via PowerShell."""
    ps_script = f"""
[Windows.UI.Notifications.ToastNotificationManager, Windows.UI.Notifications, ContentType = WindowsRuntime] | Out-Null
[Windows.Data.Xml.Dom.XmlDocument, Windows.Data.Xml.Dom.XmlDocument, ContentType = WindowsRuntime] | Out-Null
$xml = [Windows.UI.Notifications.ToastNotificationManager]::GetTemplateContent([Windows.UI.Notifications.ToastTemplateType]::ToastText02)
$elements = $xml.GetElementsByTagName('text')
$elements[0].AppendChild($xml.CreateTextNode({repr(title)})) | Out-Null
$elements[1].AppendChild($xml.CreateTextNode({repr(message)})) | Out-Null
$notifier = [Windows.UI.Notifications.ToastNotificationManager]::CreateToastNotifier('Wordle Updater')
$notifier.Show([Windows.UI.Notifications.ToastNotification]::new($xml))
"""
    try:
        subprocess.run(['powershell', '-NoProfile', '-Command', ps_script],
                       capture_output=True, timeout=10)
    except Exception:
        pass  # Don't let notification failure break the script
