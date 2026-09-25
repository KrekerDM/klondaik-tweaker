import ru from "./lang/ru.js";
import en from "./lang/en.js";

export const LANGS = [
  { id: "ru", name: "Русский" },
  { id: "uk", name: "Українська" },
  { id: "be", name: "Беларуская" },
  { id: "kk", name: "Қазақша" },
  { id: "uz", name: "Oʻzbekcha" },
  { id: "az", name: "Azərbaycan" },
  { id: "en", name: "English" },
  { id: "de", name: "Deutsch" },
  { id: "pl", name: "Polski" },
  { id: "es", name: "Español" },
  { id: "fr", name: "Français" }
];

const dict = { ru, en };
let lang = "ru";

export function known(code) {
  return LANGS.some((x) => x.id === code);
}

export async function loadLang(code) {
  if (!known(code) || dict[code]) return;
  try {
    const module = await import("./lang/" + code + ".js");
    if (module && module.default) dict[code] = module.default;
  } catch {}
}

export function setLang(value) {
  lang = known(value) ? value : "ru";
  document.documentElement.lang = lang;
}

export function getLang() {
  return lang;
}

export function t(key) {
  const table = dict[lang];
  if (table && table[key]) return table[key];
  if (en[key]) return en[key];
  if (ru[key]) return ru[key];
  return key;
}
