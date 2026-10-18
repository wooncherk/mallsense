import type { ReactNode } from 'react';

function inlineMarkdown(text: string): ReactNode[] {
  const tokens = text.split(/(\*\*[^*]+\*\*|`[^`]+`|\[[^\]]+]\(https?:\/\/[^)\s]+\))/g).filter(Boolean);
  return tokens.map((token, index) => {
    if (token.startsWith('**') && token.endsWith('**')) return <strong key={index} className="font-semibold text-base-content">{token.slice(2, -2)}</strong>;
    if (token.startsWith('`') && token.endsWith('`')) return <code key={index} className="rounded bg-base-200 px-1.5 py-0.5 text-[.9em] text-base-content">{token.slice(1, -1)}</code>;
    const link = token.match(/^\[([^\]]+)]\((https?:\/\/[^)\s]+)\)$/);
    if (link) return <a key={index} href={link[2]} target="_blank" rel="noreferrer" className="font-medium text-primary underline decoration-primary/30 underline-offset-2">{link[1]}</a>;
    return token;
  });
}

export function AgentMarkdown({ children, compact = false }: { children: string; compact?: boolean }) {
  const lines = children.replace(/\r/g, '').split('\n');
  const blocks: ReactNode[] = [];
  let index = 0;

  while (index < lines.length) {
    const line = lines[index].trim();
    if (!line) { index += 1; continue; }
    const heading = line.match(/^(#{1,3})\s+(.+)$/);
    if (heading) {
      blocks.push(<h4 key={`h-${index}`} className="mt-4 first:mt-0 text-base font-bold leading-6 text-base-content">{inlineMarkdown(heading[2])}</h4>);
      index += 1; continue;
    }
    if (/^(?:[-*+]\s+|•\s*)/.test(line)) {
      const items: string[] = [];
      while (index < lines.length && /^(?:[-*+]\s+|•\s*)/.test(lines[index].trim())) { items.push(lines[index].trim().replace(/^(?:[-*+]\s+|•\s*)/, '')); index += 1; }
      blocks.push(<ul key={`ul-${index}`} className="my-3 space-y-2 pl-5 text-sm leading-6 text-base-content/70">{items.map((item, itemIndex) => <li key={itemIndex} className="list-disc pl-1 marker:text-primary">{inlineMarkdown(item)}</li>)}</ul>);
      continue;
    }
    if (/^\d+[.)]\s+/.test(line)) {
      const items: string[] = [];
      while (index < lines.length && /^\d+[.)]\s+/.test(lines[index].trim())) { items.push(lines[index].trim().replace(/^\d+[.)]\s+/, '')); index += 1; }
      blocks.push(<ol key={`ol-${index}`} className="my-3 space-y-2 pl-5 text-sm leading-6 text-base-content/70">{items.map((item, itemIndex) => <li key={itemIndex} className="list-decimal pl-1 marker:font-semibold marker:text-primary">{inlineMarkdown(item)}</li>)}</ol>);
      continue;
    }
    if (line.startsWith('>')) {
      blocks.push(<blockquote key={`quote-${index}`} className="my-3 border-l-2 border-primary/30 pl-3 text-sm leading-6 text-base-content/60">{inlineMarkdown(line.replace(/^>\s?/, ''))}</blockquote>);
      index += 1; continue;
    }
    const paragraph: string[] = [line];
    index += 1;
    while (index < lines.length && lines[index].trim() && !/^(?:#{1,3}\s+|[-*+]\s+|•\s*|\d+[.)]\s+|>)/.test(lines[index].trim())) { paragraph.push(lines[index].trim()); index += 1; }
    blocks.push(<p key={`p-${index}`} className={`${compact ? 'my-1.5' : 'my-3'} text-sm leading-6 text-base-content/70`}>{inlineMarkdown(paragraph.join(' '))}</p>);
  }
  return <div className="agent-markdown">{blocks}</div>;
}
