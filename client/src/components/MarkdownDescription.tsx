import type { ReactNode } from 'react';

function inlineMarkdown(text: string): ReactNode[] {
  return text.split(/(\*\*[^*]+\*\*|_[^_]+_|`[^`]+`)/g).filter(Boolean).map((part, index) => {
    if (part.startsWith('**') && part.endsWith('**')) return <strong key={index}>{part.slice(2, -2)}</strong>;
    if (part.startsWith('_') && part.endsWith('_')) return <em key={index}>{part.slice(1, -1)}</em>;
    if (part.startsWith('`') && part.endsWith('`')) return <code key={index}>{part.slice(1, -1)}</code>;
    return part;
  });
}

export function MarkdownDescription({ markdown, className = '' }: { markdown: string; className?: string }) {
  const lines = markdown.split(/\r?\n/);
  return <div className={`space-y-1 ${className}`}>{lines.map((line, index) => {
    const heading = line.match(/^\s{0,3}(#{1,4})\s+(.+)$/);
    const bullet = line.match(/^\s*[-*+]\s+(.+)$/);
    if (heading) return <h4 key={index} className="font-semibold">{inlineMarkdown(heading[2])}</h4>;
    if (bullet) return <div key={index} className="flex gap-2 pl-2"><span aria-hidden="true">•</span><span>{inlineMarkdown(bullet[1])}</span></div>;
    if (!line.trim()) return <div key={index} className="h-1" />;
    return <p key={index}>{inlineMarkdown(line)}</p>;
  })}</div>;
}
