using System;
using System.Collections.Generic;

namespace NoFences.Services
{
    public static class LocalizationManager
    {
        public enum Language { English, Spanish }

        public static Language CurrentLanguage { get; set; } = Language.English;

        private static readonly Dictionary<string, Dictionary<Language, string>> Resources = new Dictionary<string, Dictionary<Language, string>>
        {
            // General
            { "General", new Dictionary<Language, string> { { Language.English, "General" }, { Language.Spanish, "General" } } },
            { "Personalization", new Dictionary<Language, string> { { Language.English, "Personalization" }, { Language.Spanish, "Personalización" } } },
            { "About", new Dictionary<Language, string> { { Language.English, "About" }, { Language.Spanish, "Acerca de" } } },
            
            // Context Menu
            { "ConfigureFences", new Dictionary<Language, string> { { Language.English, "Configure Fences..." }, { Language.Spanish, "Configurar Fences..." } } },
            { "LockFences", new Dictionary<Language, string> { { Language.English, "Lock Fences" }, { Language.Spanish, "Bloquear Fences" } } },
            { "Minify", new Dictionary<Language, string> { { Language.English, "Minify" }, { Language.Spanish, "Minimizar" } } },
            { "View", new Dictionary<Language, string> { { Language.English, "View" }, { Language.Spanish, "Ver" } } },
            { "CustomizeHeaders", new Dictionary<Language, string> { { Language.English, "Customize Headers" }, { Language.Spanish, "Personalizar Encabezados" } } },
            { "ShowHeader", new Dictionary<Language, string> { { Language.English, "Show Header" }, { Language.Spanish, "Mostrar Encabezado" } } },
            { "Alignment", new Dictionary<Language, string> { { Language.English, "Alignment:" }, { Language.Spanish, "Alineación:" } } },
            { "IconsAndText", new Dictionary<Language, string> { { Language.English, "Icons & Text" }, { Language.Spanish, "Iconos y Texto" } } },
            { "IconSize", new Dictionary<Language, string> { { Language.English, "Icon Size:" }, { Language.Spanish, "Tamaño de Icono:" } } },
            { "ChangeTitleFont", new Dictionary<Language, string> { { Language.English, "Change Title Font..." }, { Language.Spanish, "Cambiar fuente de título..." } } },
            { "ChangeItemFont", new Dictionary<Language, string> { { Language.English, "Change Item Font..." }, { Language.Spanish, "Cambiar fuente de ítems..." } } },

            // About
            { "AboutTitle", new Dictionary<Language, string> { { Language.English, "About NoFences" }, { Language.Spanish, "Acerca de NoFences" } } },
            { "CreatedBy", new Dictionary<Language, string> { { Language.English, "Created by Notbanzz" }, { Language.Spanish, "Creado por Notbanzz" } } },
            { "Description", new Dictionary<Language, string> { { Language.English, "A modern, lightweight desktop fence application." }, { Language.Spanish, "Una aplicación moderna y ligera para organizar el escritorio." } } },
            { "VisitGithub", new Dictionary<Language, string> { { Language.English, "Visit GitHub Repository" }, { Language.Spanish, "Visitar repositorio GitHub" } } },
            
            // Tray Icon
            { "NewFence", new Dictionary<Language, string> { { Language.English, "New Fence" }, { Language.Spanish, "Nuevo Fence" } } },
            { "ShowFences", new Dictionary<Language, string> { { Language.English, "Show Fences" }, { Language.Spanish, "Mostrar Fences" } } },
            { "HideFences", new Dictionary<Language, string> { { Language.English, "Hide Fences" }, { Language.Spanish, "Ocultar Fences" } } },
            { "Exit", new Dictionary<Language, string> { { Language.English, "Exit" }, { Language.Spanish, "Salir" } } },
            
            // Laptop Mode
            { "LaptopMode", new Dictionary<Language, string> { { Language.English, "Laptop Mode (Save Battery)" }, { Language.Spanish, "Modo Portátil (Ahorro Batería)" } } }
        };

        public static string GetString(string key)
        {
            if (Resources.ContainsKey(key))
            {
                return Resources[key][CurrentLanguage];
            }
            return key;
        }
    }
}
