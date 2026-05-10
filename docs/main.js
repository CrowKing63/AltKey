(() => {
  const storageKey = 'altkey-docs-theme';
  const root = document.documentElement;
  const select = document.getElementById('theme-select');
  const themeColorMeta = document.querySelector('meta[name="theme-color"]');
  const keyboardShot = document.getElementById('keyboard-shot');
  const darkMedia = window.matchMedia('(prefers-color-scheme: dark)');
  const forcedColorsMedia = window.matchMedia('(forced-colors: active)');

  const themeColors = {
    light: '#f7f3ff',
    dark: '#071818',
    'high-contrast': '#000000'
  };

  const keyboardImages = {
    light: {
      src: 'assets/AltKey_Basic_Light.png',
      width: 1126,
      height: 382,
      alt: 'AltKey 라이트 테마 기본 키보드 화면'
    },
    dark: {
      src: 'assets/AltKey_Basic_Dark.png',
      width: 1126,
      height: 382,
      alt: 'AltKey 다크 테마 기본 키보드 화면'
    },
    'high-contrast': {
      src: 'assets/AltKey_Basic_HighContrast.png',
      width: 1126,
      height: 382,
      alt: 'AltKey 고대비 테마 기본 키보드 화면'
    }
  };

  function resolveTheme(theme) {
    if (theme === 'light' || theme === 'dark' || theme === 'high-contrast') {
      return theme;
    }

    if (forcedColorsMedia.matches) {
      return 'high-contrast';
    }

    return darkMedia.matches ? 'dark' : 'light';
  }

  function updateThemeColor(theme) {
    if (!themeColorMeta) {
      return;
    }

    themeColorMeta.setAttribute('content', themeColors[theme] || themeColors.light);
  }

  function updateKeyboardImage(theme) {
    if (!keyboardShot) {
      return;
    }

    const image = keyboardImages[theme] || keyboardImages.light;
    keyboardShot.src = image.src;
    keyboardShot.width = image.width;
    keyboardShot.height = image.height;
    keyboardShot.alt = image.alt;
  }

  function applyTheme(preference, persist) {
    const resolvedTheme = resolveTheme(preference);
    root.dataset.theme = resolvedTheme;
    root.dataset.themePreference = preference;

    if (select) {
      select.value = preference;
    }

    updateThemeColor(resolvedTheme);
    updateKeyboardImage(resolvedTheme);

    if (persist) {
      localStorage.setItem(storageKey, preference);
    }
  }

  const initialPreference = root.dataset.themePreference || localStorage.getItem(storageKey) || 'system';
  applyTheme(initialPreference, false);

  if (select) {
    select.addEventListener('change', (event) => {
      applyTheme(event.target.value, true);
    });
  }

  function handleSystemThemeChange() {
    if ((root.dataset.themePreference || 'system') === 'system') {
      applyTheme('system', false);
    }
  }

  darkMedia.addEventListener('change', handleSystemThemeChange);
  forcedColorsMedia.addEventListener('change', handleSystemThemeChange);
})();
