import ru from "./lang/ru.js";
import uk from "./lang/uk.js";
import be from "./lang/be.js";
import kk from "./lang/kk.js";
import uz from "./lang/uz.js";
import az from "./lang/az.js";
import en from "./lang/en.js";
import de from "./lang/de.js";
import pl from "./lang/pl.js";
import es from "./lang/es.js";
import fr from "./lang/fr.js";

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

const dict = { ru, uk, be, kk, uz, az, en, de, pl, es, fr };
let lang = "ru";

export function known(code) {
  return Object.prototype.hasOwnProperty.call(dict, code);
}

export async function loadLang() {}

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
