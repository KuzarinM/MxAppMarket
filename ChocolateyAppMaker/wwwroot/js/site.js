// Функция обновления иконки кнопки (Солнце/Луна)
const updateThemeIcon = theme => {
    const icon = document.getElementById('theme-icon');
    if (!icon) return;

    if (theme === 'dark') {
        icon.classList.remove('bi-moon-stars-fill');
        icon.classList.add('bi-sun-fill');
    } else {
        icon.classList.remove('bi-sun-fill');
        icon.classList.add('bi-moon-stars-fill');
    }
}

// Функция установки темы (меняет атрибут, сохраняет в память, меняет иконку)
const setTheme = theme => {
    document.documentElement.setAttribute('data-bs-theme', theme);
    localStorage.setItem('theme', theme);
    updateThemeIcon(theme);
}

// Обработчик клика (вызывается по кнопке в навбаре)
// Прикрепляем к window, чтобы гарантированно видеть из HTML onclick=""
window.toggleTheme = () => {
    const currentTheme = document.documentElement.getAttribute('data-bs-theme');
    const newTheme = currentTheme === 'dark' ? 'light' : 'dark';
    setTheme(newTheme);
}

// Инициализация (запускается один раз при загрузке скрипта)
const initTheme = () => {
    const savedTheme = localStorage.getItem('theme');

    // ЛОГИКА ПО УМОЛЧАНИЮ:
    // Если в памяти ничего нет -> ставим 'dark'.
    // Если есть -> берем то, что сохранил пользователь.
    const theme = savedTheme || 'dark';

    // Важно: Атрибут data-bs-theme мы уже выставили скриптом в <head> (в _Layout),
    // поэтому здесь нам нужно ТОЛЬКО обновить иконку, чтобы она соответствовала теме.
    updateThemeIcon(theme);
}

// Запуск
initTheme();