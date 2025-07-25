
def extract_gitbook_content_fixed(document_nodes):
    """
    Extrator corrigido para conteúdo do GitBook
    Baseado na análise da estrutura real da API
    """
    texts = []
    
    def extract_from_node(node):
        if not isinstance(node, dict):
            return ""
        
        node_type = node.get('type', '')
        collected_texts = []
        
        # Estratégia 1: Nós de texto direto
        if node_type == 'text' and 'text' in node:
            text = node['text'].strip()
            if text:
                collected_texts.append(text)
        
        # Estratégia 2: Parágrafos e blocos
        elif node_type in ['paragraph', 'block', 'heading-1', 'heading-2', 'heading-3']:
            # Procurar por texto em children/nodes
            for child_field in ['children', 'nodes', 'content']:
                if child_field in node and isinstance(node[child_field], list):
                    for child in node[child_field]:
                        child_text = extract_from_node(child)
                        if child_text:
                            collected_texts.append(child_text)
        
        # Estratégia 3: Campos de texto direto
        text_fields = ['text', 'content', 'value', 'plainText']
        for field in text_fields:
            if field in node and isinstance(node[field], str):
                text = node[field].strip()
                if text and text not in collected_texts:
                    collected_texts.append(text)
        
        # Estratégia 4: Recursão em estruturas aninhadas
        if not collected_texts:
            for key, value in node.items():
                if key in ['children', 'nodes', 'content'] and isinstance(value, list):
                    for item in value:
                        item_text = extract_from_node(item)
                        if item_text:
                            collected_texts.append(item_text)
        
        return ' '.join(collected_texts)
    
    # Processar todos os nós
    if isinstance(document_nodes, list):
        for node in document_nodes:
            node_text = extract_from_node(node)
            if node_text:
                texts.append(node_text)
    
    return ' '.join(texts)

# Exemplo de uso:
# content = extract_gitbook_content_fixed(api_response['document']['nodes'])
