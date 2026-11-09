// The host aliases window.sessionStorage to localStorage (a patch for the authentication library); the real storage of the tab is captured before it
const storage = () => window.__floxTabStorage || window.sessionStorage;

export const getItem = key => storage().getItem(key);
export const setItem = (key, value) => storage().setItem(key, value);
export const removeItem = key => storage().removeItem(key);
