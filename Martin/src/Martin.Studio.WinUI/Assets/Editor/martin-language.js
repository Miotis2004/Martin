(function () {
  'use strict';

  const keywords = ['as', 'break', 'continue', 'else', 'false', 'for', 'func', 'if', 'import', 'let', 'module', 'mut', 'return', 'struct', 'true', 'var', 'while'];

  window.MartinLanguage = {
    register(monaco) {
      monaco.languages.register({ id: 'martin', extensions: ['.martin'], aliases: ['Martin', 'martin'] });
      monaco.languages.setMonarchTokensProvider('martin', {
        keywords,
        tokenizer: {
          root: [
            [/\/\/.*$/, 'comment'],
            [/"([^"\\]|\\.)*$/, 'string.invalid'],
            [/"([^"\\]|\\.)*"/, 'string'],
            [/\b\d+(\.\d+)?\b/, 'number'],
            [/[{}()[\]]/, '@brackets'],
            [/[a-zA-Z_][\w]*/, { cases: { '@keywords': 'keyword', '@default': 'identifier' } }]
          ]
        }
      });
      monaco.editor.defineTheme('martin-dark', {
        base: 'vs-dark',
        inherit: true,
        rules: [{ token: 'keyword', foreground: '569cd6' }, { token: 'comment', foreground: '6a9955' }],
        colors: { 'editor.background': '#1e1e1e' }
      });
    }
  };
}());
