import i18n from 'i18next';
import { initReactI18next } from 'react-i18next';

const resources = {
    EN: {
        translation: {
            tabs: { config: 'Config', profiles: 'Profiles', tools: 'Tools', settings: 'Settings' },
            settings: {
                title: 'Settings',
                language: 'Language',
                theme: 'Theme',
                dark: 'Dark',
                light: 'Light',
                save: 'Save Settings',
                saved: 'Settings saved!',
                loading: 'Loading settings...',
            },
        },
    },
    CZ: {
        translation: {
            tabs: { config: 'Konfigurace', profiles: 'Profily', tools: 'Nástroje', settings: 'Nastavení' },
            settings: {
                title: 'Nastavení',
                language: 'Jazyk',
                theme: 'Motiv',
                dark: 'Tmavý',
                light: 'Světlý',
                save: 'Uložit nastavení',
                saved: 'Nastavení uloženo!',
                loading: 'Načítání nastavení...',
            },
        },
    },
    UA: {
        translation: {
            tabs: { config: 'Конфігурація', profiles: 'Профілі', tools: 'Інструменти', settings: 'Налаштування' },
            settings: {
                title: 'Налаштування',
                language: 'Мова',
                theme: 'Тема',
                dark: 'Темна',
                light: 'Світла',
                save: 'Зберегти',
                saved: 'Налаштування збережено!',
                loading: 'Завантаження...',
            },
        },
    },
};

i18n.use(initReactI18next).init({
    resources,
    lng: 'UA',
    fallbackLng: 'EN',
    interpolation: { escapeValue: false },
});

export default i18n;