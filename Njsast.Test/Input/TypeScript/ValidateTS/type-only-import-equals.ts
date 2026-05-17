import Rule = editor.languages.Rule;
import Language = editor.languages.Language;

export interface Tokenizer {
    [name: string]: Rule[];
}

type ExtendedLanguage = {
    keywords: string[];
} & Language;

export function createLanguage(): ExtendedLanguage {
    return {
        keywords: []
    };
}
