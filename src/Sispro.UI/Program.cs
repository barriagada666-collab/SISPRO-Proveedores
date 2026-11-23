using Sispro.Core.Dtos;
using Sispro.Services;
using Sispro.UI.Forms;
using Sispro.UI.Theme;

namespace Sispro.UI;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        Application.ThreadException += (_, e) =>
            CambiasoTheme.ShowError(null, $"Se produjo un error inesperado:\n{e.Exception.Message}");

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                CambiasoTheme.ShowError(null, $"Error crítico de aplicación:\n{ex.Message}");
            }
        };

        using SisproApplicationContext context = InitializeContextWithSplash();

        while (true)
        {
            using LoginForm login = new(context);
            if (login.ShowDialog() != DialogResult.OK || login.AuthenticatedSession is null)
            {
                break;
            }

            using MainForm main = new(context, login.AuthenticatedSession);
            Application.Run(main);

            if (!main.LogoutRequested)
            {
                break;
            }

            context.CurrentSession = null;
        }
    }

    private static SisproApplicationContext InitializeContextWithSplash()
    {
        using SplashForm splash = new();
        splash.Show();
        splash.SetProgress(25, "Cargando configuración corporativa...");
        AppSettings settings = AppSettingsStore.Load();

        splash.SetProgress(65, "Inicializando servicios, grupos y motor SQLite WAL...");
        SisproApplicationContext context = new(settings);

        splash.SetProgress(100, "Sistema listo.");
        Thread.Sleep(250);
        splash.Close();
        return context;
    }
}
