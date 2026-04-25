const themeType = {
  obc2016: 'obc2016',
  light: 'light',
  dark: 'dark',
  default: 'light',
};

const isLocalStorageAvailable = (() => {
  if (typeof window === 'undefined' || !window.localStorage) return false;
  return true;
})();

const getTheme = () => {
  if (!isLocalStorageAvailable) return themeType.default;
  const savedTheme = localStorage.getItem('rbx_theme_v1');
  return Object.values(themeType).includes(savedTheme) ? savedTheme : themeType.default;
};

const darkmode = async () => {
  if (typeof window === 'undefined') return;
  
  const DarkReader = await import('darkreader');
  
  DarkReader.setFetchMethod(window.fetch);
  DarkReader.enable({
    brightness: 100,
    contrast: 100,
    sepia: 0
  }, {
    engine: 1 
  });
};

const nodarkmode = async () => {
  if (typeof window === 'undefined') return;
  const DarkReader = await import('darkreader');
  DarkReader.disable();
};

const setTheme = (theme) => {
  if (!isLocalStorageAvailable || !Object.values(themeType).includes(theme)) return;
  
  localStorage.setItem('rbx_theme_v1', theme);
  apply(theme);
};

const apply = (theme) => {
  if (typeof document === 'undefined') return;
  
  const html = document.documentElement;

  Object.values(themeType).forEach((t) => html.classList.remove(t));
  
  html.classList.add(theme);
  
  if (theme === 'dark') {
    darkmode();
  } else {
    nodarkmode();
  }
};

if (typeof window !== 'undefined') {
  if (document.readyState === 'complete') {
    apply(getTheme());
  } else {
    window.addEventListener('load', () => apply(getTheme()));
  }
}

export { themeType, getTheme, setTheme };
