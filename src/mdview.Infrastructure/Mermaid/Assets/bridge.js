const engine = globalThis.mermaid;
const config = {
  startOnLoad: false, securityLevel: 'strict', theme: 'dark',
  htmlLabels: false, flowchart: { htmlLabels: false },
  fontFamily: 'Arial, Hiragino Sans, sans-serif',
  maxTextSize: 50000, maxEdges: 500, suppressErrorRendering: true
};
const post = value => globalThis.invokeCSharpAction(JSON.stringify(value));
globalThis.mdviewRender = async (id, source) => {
  try {
    engine.initialize(config);
    await document.fonts.ready;
    const result = await engine.render('diagram' + id, source);
    // Resolve browser CSS into static SVG presentation attributes. This also
    // removes Mermaid's bundled animation rules and CSS color expressions.
    const host = document.createElement('div');
    host.innerHTML = result.svg;
    document.body.appendChild(host);
    const svg = host.querySelector('svg');
    const paints = ['fill', 'fill-opacity', 'stroke', 'stroke-opacity', 'stroke-width',
      'stroke-dasharray', 'stroke-dashoffset', 'stroke-linecap', 'stroke-linejoin',
      'opacity', 'font-family', 'font-size', 'font-weight', 'font-style',
      'text-anchor', 'dominant-baseline', 'visibility', 'display',
      'marker-start', 'marker-mid', 'marker-end'];
    // Mermaid's nested tspan positioning differs between WebKit and SVG loaders.
    // Keep text as vectors, but use WebKit's measured baseline for each text run.
    for (const text of [...svg.querySelectorAll('text')]) {
      const group = document.createElementNS(svg.namespaceURI, 'g');
      if (text.hasAttribute('transform')) group.setAttribute('transform', text.getAttribute('transform'));
      const walker = document.createTreeWalker(text, NodeFilter.SHOW_TEXT);
      let node, offset = 0;
      while ((node = walker.nextNode())) {
        const content = node.textContent;
        if (!content.length) continue;
        if (offset < text.getNumberOfChars()) {
          const position = text.getStartPositionOfChar(offset);
          const run = document.createElementNS(svg.namespaceURI, 'text');
          const style = getComputedStyle(node.parentElement);
          for (const name of paints) run.setAttribute(name, style.getPropertyValue(name));
          run.setAttribute('text-anchor', 'start');
          run.setAttribute('dominant-baseline', 'auto');
          run.setAttribute('x', position.x);
          run.setAttribute('y', position.y);
          run.setAttribute('xml:space', 'preserve');
          run.textContent = content;
          group.appendChild(run);
        }
        offset += content.length;
      }
      // Compute other styles while the original selector hierarchy still exists.
      text._replacement = group;
    }
    const resolved = [...svg.querySelectorAll('*'), svg].map(element => {
      const style = getComputedStyle(element);
      return [element, paints.map(name => [name, style.getPropertyValue(name)])];
    });
    for (const [element, attributes] of resolved) {
      element.removeAttribute('style');
      for (const [name, raw] of attributes) {
        const value = raw.replace(/url\(["']?[^)]*#([^"')]+)["']?\)/g, 'url(#$1)');
        if (value) element.setAttribute(name, value);
      }
    }
    svg.querySelectorAll('text').forEach(text => {
      if (text._replacement) text.replaceWith(text._replacement);
    });
    svg.querySelectorAll('style').forEach(element => element.remove());
    post({ id, svg: new XMLSerializer().serializeToString(svg) });
  } catch (_) {
    post({ id, error: '図を生成できません。Mermaid の構文を確認してください。' });
  } finally {
    document.querySelectorAll('body > div').forEach(element => element.remove());
  }
};
// The native message bridge becomes usable after NavigationCompleted.
