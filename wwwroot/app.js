/* global WebViewer, signalR */

const query = new URLSearchParams(window.location.search);
const documentId = query.get('documentId') || 'webviewer-demo-1';
const documentUrl = query.get('documentUrl') || '/sample-files/document.pdf';
const authorInput = document.getElementById('author');
const statusElement = document.getElementById('status');
const randomAuthorId = typeof crypto.randomUUID === 'function'
  ? crypto.randomUUID()
  : Array.from(crypto.getRandomValues(new Uint8Array(4)), (byte) => byte.toString(16).padStart(2, '0')).join('');
const generatedAuthor = `user-${randomAuthorId.slice(0, 8)}`;
authorInput.value = sessionStorage.getItem('collaboration-author') || generatedAuthor;

let annotationManager;
let connection;
const revisions = new Map();

authorInput.addEventListener('change', () => {
  const author = authorInput.value.trim() || generatedAuthor;
  authorInput.value = author;
  sessionStorage.setItem('collaboration-author', author);
  annotationManager?.setCurrentUser(author);
});

const setStatus = (value) => {
  statusElement.textContent = value;
};

const wrapCommand = (annotationXml, operation) => {
  const commands = {
    add: `<add>${annotationXml}</add><modify /><delete />`,
    modify: `<add /><modify>${annotationXml}</modify><delete />`,
    delete: `<add /><modify /><delete>${annotationXml}</delete>`,
  };
  return `<?xml version="1.0" encoding="UTF-8"?><xfdf xmlns="http://ns.adobe.com/xfdf/" xml:space="preserve"><fields />${commands[operation]}</xfdf>`;
};

const publishCommand = async () => {
  const command = await annotationManager.exportAnnotationCommand();
  const xml = new DOMParser().parseFromString(command, 'text/xml');
  const serializer = new XMLSerializer();

  for (const operation of ['add', 'modify', 'delete']) {
    for (const node of xml.getElementsByTagName(operation)[0]?.children || []) {
      await connection.invoke('PublishAnnotation', {
        documentId,
        annotationId: node.getAttribute('name'),
        operation,
        xfdf: wrapCommand(serializer.serializeToString(node), operation),
        authorId: authorInput.value,
      });
    }
  }
};

const applyChange = async (change) => {
  const currentRevision = revisions.get(change.annotationId) || 0;
  if (change.revision <= currentRevision) {
    return;
  }

  revisions.set(change.annotationId, change.revision);
  const imported = await annotationManager.importAnnotationCommand(change.xfdf);
  await annotationManager.drawAnnotationsFromList(imported);
};

WebViewer(
  {
    path: '/lib',
    initialDoc: documentUrl,
  },
  document.getElementById('viewer'),
).then((instance) => {
  annotationManager = instance.Core.annotationManager;
  annotationManager.setCurrentUser(authorInput.value);

  instance.Core.documentViewer.addEventListener('documentLoaded', async () => {
    connection = new signalR.HubConnectionBuilder()
      .withUrl(`/hubs/collaboration?authorId=${encodeURIComponent(authorInput.value)}`)
      .withAutomaticReconnect()
      .build();

    connection.on('AnnotationChanged', applyChange);
    connection.onreconnecting(() => setStatus('Reconnecting'));
    connection.onreconnected(async () => {
      const snapshot = await connection.invoke('JoinDocument', documentId);
      await Promise.all(snapshot.map(applyChange));
      setStatus('Connected');
    });
    connection.onclose(() => setStatus('Offline'));

    await connection.start();
    const snapshot = await connection.invoke('JoinDocument', documentId);
    for (const change of snapshot) {
      await applyChange(change);
    }
    setStatus('Connected');
  });

  annotationManager.addEventListener('annotationChanged', async (_annotations, _operation, info = {}) => {
    if (!info.imported && connection?.state === signalR.HubConnectionState.Connected) {
      await publishCommand();
    }
  });
});