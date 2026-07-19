(function () {
  'use strict';

  const martinEditor = {
    editor: null,
    models: new Map(),
    activeDocumentId: null,
    ready: false,
    monacoVersion: '0.52.2'
  };

  function post(type, payload, id) {
    if (window.chrome && window.chrome.webview) {
      window.chrome.webview.postMessage({ type, id: id || null, payload: payload || {} });
    }
  }

  function validateMessage(message) {
    if (!message || typeof message.type !== 'string' || message.type.trim().length === 0) {
      throw new Error('Editor host message type is required.');
    }
    return message;
  }

  function uriFor(documentId, path) {
    const safePath = (path || documentId).replace(/\\/g, '/').replace(/^([A-Za-z]):/, '$1');
    return monaco.Uri.parse('martin://workspace/' + safePath.replace(/^\/+/, ''));
  }

  function attachChangeListener(documentId, entry) {
    entry.model.onDidChangeContent(function (event) {
      if (!entry.suppressChange) {
        const previousVersion = entry.version;
        const newVersion = previousVersion + 1;
        entry.version = newVersion;
        post('textChanged', {
          documentId: documentId,
          previousVersion: previousVersion,
          newVersion: newVersion,
          changes: event.changes.map(function (change) {
            return { rangeOffset: change.rangeOffset, rangeLength: change.rangeLength, text: change.text };
          })
        });
      }
    });
  }

  function recreateModelForPathChange(documentId, entry, path, text, language) {
    const wasActive = martinEditor.activeDocumentId === documentId;
    if (wasActive) {
      entry.viewState = martinEditor.editor.saveViewState();
      martinEditor.editor.setModel(null);
    }

    const oldModel = entry.model;
    const markers = monaco.editor.getModelMarkers({ resource: oldModel.uri });
    oldModel.dispose();

    entry.path = path;
    entry.model = monaco.editor.createModel(text, language, uriFor(documentId, path));
    attachChangeListener(documentId, entry);
    if (markers.length > 0) {
      monaco.editor.setModelMarkers(entry.model, 'martin', markers);
    }
  }

  function openDocument(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    if (!documentId) {
      throw new Error('openDocument requires documentId.');
    }

    const text = String(payload.text || payload.Text || '');
    const language = String(payload.language || payload.Language || 'martin');
    const path = String(payload.path || payload.Path || documentId);
    let entry = martinEditor.models.get(documentId);
    if (!entry) {
      entry = {
        path: path,
        readOnly: false,
        version: Number(payload.version || payload.Version || 0),
        model: monaco.editor.createModel(text, language, uriFor(documentId, path)),
        viewState: null,
        suppressChange: false
      };
      attachChangeListener(documentId, entry);
      martinEditor.models.set(documentId, entry);
    } else {
      const requestedUri = uriFor(documentId, path).toString();
      if (entry.model.uri.toString() !== requestedUri) {
        recreateModelForPathChange(documentId, entry, path, text, language);
      } else {
        entry.path = path;
        if (entry.model.getValue() !== text) {
          entry.suppressChange = true;
          try {
            entry.model.setValue(text);
          } finally {
            entry.suppressChange = false;
          }
        }
      }
      entry.version = Number(payload.version || payload.Version || entry.version);
    }
    activateDocument({ documentId: documentId });
    const viewState = payload.viewState || payload.ViewState;
    if (viewState) { restoreViewState(viewState); }
  }

  function activateDocument(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (!entry) {
      throw new Error('Cannot activate unknown document.');
    }
    if (martinEditor.activeDocumentId && martinEditor.activeDocumentId !== documentId) {
      const previous = martinEditor.models.get(martinEditor.activeDocumentId);
      if (previous) { previous.viewState = martinEditor.editor.saveViewState(); }
    }
    martinEditor.activeDocumentId = documentId;
    martinEditor.editor.setModel(entry.model);
    martinEditor.editor.updateOptions({ readOnly: entry.readOnly === true });
    if (entry.viewState) { martinEditor.editor.restoreViewState(entry.viewState); }
    martinEditor.editor.focus();
  }

  function closeDocument(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (entry) {
      if (martinEditor.editor.getModel() === entry.model) {
        martinEditor.editor.setModel(null);
      }
      entry.model.dispose();
      martinEditor.models.delete(documentId);
    }
  }

  function saveActiveViewState() {
    if (!martinEditor.activeDocumentId) return;
    const entry = martinEditor.models.get(martinEditor.activeDocumentId);
    if (!entry) return;
    entry.viewState = martinEditor.editor.saveViewState();
    const position = martinEditor.editor.getPosition() || { lineNumber: 1, column: 1 };
    const scroll = martinEditor.editor.getScrollPosition();
    post('viewStateChanged', { documentId: martinEditor.activeDocumentId, cursorLine: position.lineNumber, cursorColumn: position.column, scrollTop: scroll.scrollTop, scrollLeft: scroll.scrollLeft });
  }

  function revealRange(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    activateDocument({ documentId: documentId });
    const range = new monaco.Range(payload.startLine || payload.StartLine || 1, payload.startColumn || payload.StartColumn || 1, payload.endLine || payload.EndLine || 1, payload.endColumn || payload.EndColumn || 1);
    if (payload.select === false || payload.Select === false) { martinEditor.editor.setPosition({ lineNumber: range.startLineNumber, column: range.startColumn }); }
    else { martinEditor.editor.setSelection(range); }
    martinEditor.editor.revealRangeInCenter(range);
    saveActiveViewState();
  }

  function requestText(payload, id) {
    const documentId = String(payload.documentId || payload.DocumentId || martinEditor.activeDocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (!entry) {
      throw new Error('Cannot read unknown document.');
    }
    post('response', { documentId: documentId, text: entry.model.getValue(), version: entry.version }, id);
  }

  function setTheme(payload) {
    const highContrast = payload.highContrast === true || payload.HighContrast === true || window.matchMedia('(forced-colors: active)').matches;
    const effective = String(payload.effectiveTheme || payload.EffectiveTheme || payload.theme || payload.Theme || 'Dark').toLowerCase();
    const themeName = effective === 'light' && !highContrast ? 'martin-light' : 'martin-dark';
    document.documentElement.dataset.theme = highContrast ? 'high-contrast' : themeName;
    monaco.editor.setTheme(themeName);
    if (martinEditor.editor) {
      martinEditor.editor.updateOptions({ accessibilitySupport: highContrast ? 'on' : 'auto' });
    }
  }

  function setMarkers(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (!entry) return;
    const markers = (payload.markers || payload.Markers || []).map(function (marker) {
      return {
        startLineNumber: marker.startLine || marker.StartLine || 1,
        startColumn: marker.startColumn || marker.StartColumn || 1,
        endLineNumber: marker.endLine || marker.EndLine || 1,
        endColumn: marker.endColumn || marker.EndColumn || 1,
        severity: marker.severity === 'Error' ? monaco.MarkerSeverity.Error : (marker.severity === 'Info' || marker.severity === 'Information' ? monaco.MarkerSeverity.Info : (marker.severity === 'Hint' ? monaco.MarkerSeverity.Hint : monaco.MarkerSeverity.Warning)),
        message: marker.message || marker.Message || '',
        code: marker.code || marker.Code || undefined
      };
    });
    monaco.editor.setModelMarkers(entry.model, 'martin', markers);
  }

  function replaceText(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (!entry) { throw new Error('Cannot replace text for unknown document.'); }
    const text = String(payload.text || payload.Text || '');
    if (entry.model.getValue() !== text) {
      entry.suppressChange = true;
      try {
        entry.model.setValue(text);
      } finally {
        entry.suppressChange = false;
      }
      entry.version += 1;
    }
  }

  function clearMarkers(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (entry) { monaco.editor.setModelMarkers(entry.model, 'martin', []); }
  }

  function setReadOnly(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    const entry = martinEditor.models.get(documentId);
    if (!entry) { throw new Error('Cannot set read-only for unknown document.'); }
    entry.readOnly = payload.isReadOnly === true || payload.IsReadOnly === true;
    if (martinEditor.activeDocumentId === documentId) {
      martinEditor.editor.updateOptions({ readOnly: entry.readOnly });
    }
  }

  function restoreViewState(payload) {
    const documentId = String(payload.documentId || payload.DocumentId || '');
    activateDocument({ documentId: documentId });
    const cursorLine = payload.cursorLine || payload.CursorLine || 1;
    const cursorColumn = payload.cursorColumn || payload.CursorColumn || 1;
    martinEditor.editor.setPosition({ lineNumber: cursorLine, column: cursorColumn });
    martinEditor.editor.setScrollPosition({ scrollTop: payload.scrollTop || payload.ScrollTop || 0, scrollLeft: payload.scrollLeft || payload.ScrollLeft || 0 });
    const entry = martinEditor.models.get(documentId);
    if (entry) { entry.viewState = martinEditor.editor.saveViewState(); }
  }

  function focusEditor() {
    if (martinEditor.editor) { martinEditor.editor.focus(); }
  }

  function executeCommand(payload) {
    const command = String(payload.command || payload.Command || '');
    const commands = {
      undo: 'undo', redo: 'redo', cut: 'editor.action.clipboardCutAction',
      copy: 'editor.action.clipboardCopyAction', paste: 'editor.action.clipboardPasteAction',
      find: 'actions.find', replace: 'editor.action.startFindReplaceAction', selectAll: 'editor.action.selectAll'
    };
    if (command === 'focus') { focusEditor(); post('requestCompleted', { command: command }); return; }
    const monacoCommand = commands[command];
    if (!monacoCommand) { throw new Error('Unsupported editor command: ' + command); }
    martinEditor.editor.trigger('martin', monacoCommand, null);
    post('requestCompleted', { command: command });
  }

  // Monaco cannot open project files which do not have a model yet. Route every
  // semantic location through Studio so the workspace remains the authority for
  // opening/activating documents and revealing the requested range.
  function openCodeEditor(input, source) {
    const selection = input && input.options && input.options.selection;
    const resource = input && input.resource;
    if (!resource || !selection) { return Promise.resolve(null); }
    post('navigationRequested', {
      filePath: resource.fsPath,
      startLine: selection.startLineNumber,
      startColumn: selection.startColumn,
      endLine: selection.endLineNumber,
      endColumn: selection.endColumn
    });
    return Promise.resolve(source || martinEditor.editor);
  }

  function handleHostMessage(event) {
    try {
      const message = validateMessage(typeof event.data === 'string' ? JSON.parse(event.data) : event.data);
      const payload = message.payload || {};
      switch (message.type) {
        case 'openDocument': openDocument(payload); break;
        case 'activateDocument': activateDocument(payload); break;
        case 'closeDocument': closeDocument(payload); break;
        case 'replaceText': replaceText(payload); break;
        case 'setReadOnly': setReadOnly(payload); break;
        case 'restoreViewState': restoreViewState(payload); break;
        case 'clearMarkers': clearMarkers(payload); break;
        case 'focusEditor': focusEditor(); break;
        case 'executeCommand': executeCommand(payload); break;
        case 'revealRange': revealRange(payload); break;
        case 'requestText': requestText(payload, message.id); break;
        case 'setTheme': setTheme(payload); break;
        case 'setMarkers': setMarkers(payload); break;
        default: throw new Error('Unsupported editor host message: ' + message.type);
      }
    } catch (error) {
      post('editorError', { message: error.message || 'Editor bridge failed.', detail: error.stack || null });
    }
  }

  function start() {
    if (!window.require) {
      post('editorError', { message: 'Monaco loader is unavailable.' });
      return;
    }

    window.require.config({ paths: { vs: 'monaco/min/vs' } });
    window.require(['vs/editor/editor.main'], function () {
      if (window.MartinLanguage) {
        window.MartinLanguage.register(monaco);
      }
      if (window.MartinProviders) {
        window.MartinProviders.register(monaco, martinEditor);
      }

      martinEditor.editor = monaco.editor.create(document.getElementById('editor'), {
        automaticLayout: true,
        language: 'martin',
        theme: 'martin-dark',
        minimap: { enabled: false },
        scrollBeyondLastLine: false,
        accessibilitySupport: window.matchMedia('(forced-colors: active)').matches ? 'on' : 'auto'
      }, { codeEditorService: { openCodeEditor: openCodeEditor } });
      martinEditor.ready = true;
      martinEditor.editor.addCommand(monaco.KeyMod.CtrlCmd | monaco.KeyCode.KeyS, function () { post('saveRequested', { documentId: martinEditor.activeDocumentId }); });
      martinEditor.editor.onDidChangeCursorPosition(function () { saveActiveViewState(); post('cursorChanged', { documentId: martinEditor.activeDocumentId }); });
      martinEditor.editor.onDidScrollChange(saveActiveViewState);
      martinEditor.editor.onDidChangeCursorSelection(function () { saveActiveViewState(); post('selectionChanged', { documentId: martinEditor.activeDocumentId }); });
      post('editorReady', { monacoVersion: martinEditor.monacoVersion });
    });
  }

  window.MartinEditor = martinEditor;
  window.addEventListener('beforeunload', function () {
    if (window.MartinProviders) { window.MartinProviders.dispose(); }
  });
  if (window.chrome && window.chrome.webview) {
    window.chrome.webview.addEventListener('message', handleHostMessage);
  }
  window.addEventListener('DOMContentLoaded', start);
}());
