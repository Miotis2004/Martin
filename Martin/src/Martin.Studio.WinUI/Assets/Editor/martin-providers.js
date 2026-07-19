(function () {
  'use strict';

  const MAX_RESULTS = 1000;
  const pending = new Map();
  let sequence = 0;
  let disposables = [];
  let editorState;
  let monacoApi;

  function post(type, payload, id) {
    window.chrome.webview.postMessage({ type: type, id: id || null, payload: payload || {} });
  }

  function entryFor(model) {
    for (const pair of editorState.models) if (pair[1].model === model) return { id: pair[0], entry: pair[1] };
    return null;
  }

  function request(type, model, position, extra, token) {
    const found = entryFor(model);
    if (!found || token.isCancellationRequested) return Promise.resolve(null);
    const id = 'language-' + (++sequence);
    const payload = Object.assign({
      requestId: id, documentId: found.id, documentVersion: found.entry.version, modelVersion: model.getVersionId(),
      line: position ? position.lineNumber : 1, column: position ? position.column : 1
    }, extra || {});
    return new Promise(function (resolve) {
      const cancellation = token.onCancellationRequested(function () {
        pending.delete(id); cancellation.dispose(); post('language/cancelRequest', { requestId: id }, id); resolve(null);
      });
      pending.set(id, { resolve: resolve, cancellation: cancellation, model: model, modelVersion: model.getVersionId() });
      post(type, payload, id);
    });
  }

  function range(value) {
    return new monacoApi.Range(value.startLine, value.startColumn, value.endLine, value.endColumn);
  }

  function location(item) {
    // Use the semantic service's canonical path for every result. The editor's
    // codeEditorService routes this URI back to Studio, including when no Monaco
    // model exists for the target document yet.
    return { uri: monacoApi.Uri.file(item.filePath), range: range(item.range) };
  }

  function markdown(value) { return { value: String(value || ''), isTrusted: false, supportHtml: false }; }

  function accept(event) {
    const message = typeof event.data === 'string' ? JSON.parse(event.data) : event.data;
    if (!message || typeof message.type !== 'string' || message.type.indexOf('language/') !== 0) return;
    const item = pending.get(String(message.id || (message.payload && message.payload.requestId) || ''));
    if (!item) return;
    pending.delete(String(message.id || message.payload.requestId)); item.cancellation.dispose();
    const payload = message.payload || {};
    const current = entryFor(item.model);
    if (payload.isStale || payload.isCancelled || item.model.isDisposed() || !current || item.model.getVersionId() !== item.modelVersion || item.model.getVersionId() !== payload.modelVersion) item.resolve(null);
    else item.resolve(payload);
  }

  function register(monaco, state) {
    dispose(); monacoApi = monaco; editorState = state;
    if (window.chrome && window.chrome.webview) window.chrome.webview.addEventListener('message', accept);
    disposables = [
      monaco.languages.registerCompletionItemProvider('martin', { triggerCharacters: ['.'], provideCompletionItems: async function (m, p, c, t) {
        const r = await request('language/requestCompletion', m, p, { triggerCharacter: c.triggerCharacter || null, maximumResults: 200 }, t);
        return r && { suggestions: (r.items || []).slice(0, MAX_RESULTS).map(function (x) { return { label: x.label, kind: monaco.languages.CompletionItemKind[x.kind] || monaco.languages.CompletionItemKind.Variable, detail: x.detail, documentation: markdown(x.documentation), insertText: x.insertText, insertTextRules: x.isSnippet ? monaco.languages.CompletionItemInsertTextRule.InsertAsSnippet : undefined, range: range(x.range), commitCharacters: x.commitCharacters }; }) };
      }}),
      monaco.languages.registerHoverProvider('martin', { provideHover: async function (m, p, t) { const r = await request('language/requestHover', m, p, null, t); return r && r.hover ? { range: range(r.hover.range), contents: [markdown(r.hover.markdown)] } : null; }}),
      monaco.languages.registerDefinitionProvider('martin', { provideDefinition: async function (m, p, t) { const r = await request('language/requestDefinition', m, p, null, t); return r ? (r.locations || []).map(location) : null; }}),
      monaco.languages.registerReferenceProvider('martin', { provideReferences: async function (m, p, c, t) { const r = await request('language/requestReferences', m, p, { includeDeclaration: c.includeDeclaration === true, maximumResults: MAX_RESULTS }, t); return r ? (r.locations || []).map(location) : null; }}),
      monaco.languages.registerSignatureHelpProvider('martin', { signatureHelpTriggerCharacters: ['(', ','], signatureHelpRetriggerCharacters: [','], provideSignatureHelp: async function (m, p, t, c) { const r = await request('language/requestSignatureHelp', m, p, { triggerCharacter: c.triggerCharacter || null, isRetrigger: c.isRetrigger === true }, t); return r && r.signatureHelp ? { value: r.signatureHelp, dispose: function () {} } : null; }}),
      monaco.languages.registerDocumentFormattingEditProvider('martin', { provideDocumentFormattingEdits: async function (m, o, t) { const r = await request('language/requestFormatting', m, null, { tabSize: o.tabSize, insertSpaces: o.insertSpaces }, t); return r ? (r.edits || []).map(function (x) { return { range: range(x.range), text: x.text }; }) : []; }}),
      monaco.languages.registerDocumentSemanticTokensProvider('martin', { getLegend: function () { return { tokenTypes: ['keyword','comment','number','string','operator','type','struct','class','enum','enumMember','interface','function','method','constructor','property','parameter','variable'], tokenModifiers: ['declaration','definition','readonly','static','defaultLibrary','documentation','unresolved'] }; }, provideDocumentSemanticTokens: async function (m, _last, t) { const r = await request('language/requestSemanticTokens', m, null, null, t); return r ? { data: new Uint32Array(r.data || []), resultId: String(r.documentVersion) } : null; }, releaseDocumentSemanticTokens: function () {} })
    ];
  }

  function dispose() {
    disposables.forEach(function (x) { x.dispose(); }); disposables = [];
    pending.forEach(function (x) { x.cancellation.dispose(); x.resolve(null); }); pending.clear();
    if (window.chrome && window.chrome.webview) window.chrome.webview.removeEventListener('message', accept);
  }

  window.MartinProviders = { register: register, dispose: dispose };
}());
