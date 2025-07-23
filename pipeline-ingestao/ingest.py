import os
import requests
import base64
from bs4 import BeautifulSoup
from openai import AzureOpenAI
from azure.core.credentials import AzureKeyCredential
from azure.search.documents.indexes import SearchIndexClient
from azure.search.documents import SearchClient
from azure.search.documents.indexes.models import (
    SearchIndex,
    SearchField,
    SearchFieldDataType,
    VectorSearch,
    VectorSearchProfile,
    SimpleField,
    SearchableField
)

# --- CONFIGURAÇÃO FINAL COM SUAS CREDENCIAIS ---
devops_org = "AmbienteDeTeste1"
devops_project = "kevrau"
devops_wiki_name = "kevrau.wiki"
devops_pat = "BhCx27N9Gnw51NqM4hqruHFicj6jozEJ1VgwxOuZQnw549z655WWJQQJ99BGACAAAAAAAAAAAAASAZDO1iEr"

openai_endpoint = "https://chatbot-openai-kevin.openai.azure.com/"
openai_key = "G0ANfZeRXM8bBRCR7fbJXVSAiSZnA07jI3qrlGBWZ5kECXE1Ug08JQQJ99BGACYeBjFXJ3w3AAABACOGGeIH"
openai_api_version = "2024-03-01-preview"
embedding_deployment_name = "embedding-kevun"

search_endpoint = "https://chatbot-kevin.search.windows.net/"
search_key = "t3U8XWn2iRw9bGPe90OwXS8QUFamo7ZhSpUBMMetMQAzSeAgkCNU"
index_name = "indice-suporte-tecnico"
# ----------------------------------------------------

print("Configurando clientes...")
openai_client = AzureOpenAI(api_key=openai_key, api_version=openai_api_version, azure_endpoint=openai_endpoint)
index_client = SearchIndexClient(endpoint=search_endpoint, credential=AzureKeyCredential(search_key))
search_client = SearchClient(endpoint=search_endpoint, index_name=index_name, credential=AzureKeyCredential(search_key))

def create_search_index():
    try:
        if index_name in index_client.list_index_names():
            print(f"O índice '{index_name}' já existe. Apagando para recriar com a nova fonte de dados...")
            index_client.delete_index(index_name)
    except Exception as e:
        print(f"Aviso ao tentar apagar o índice antigo: {e}")

    print(f"Criando o índice '{index_name}'...")
    fields = [
        SimpleField(name="id", type=SearchFieldDataType.String, key=True),
        SearchableField(name="source", type=SearchFieldDataType.String, filterable=True, sortable=True),
        SearchableField(name="content", type=SearchFieldDataType.String),
        SearchField(name="content_vector", type=SearchFieldDataType.Collection(SearchFieldDataType.Single),
                    searchable=True, vector_search_dimensions=1536, vector_search_profile_name="my-hnsw-profile")
    ]
    vector_search = VectorSearch(
        profiles=[VectorSearchProfile(name="my-hnsw-profile", algorithm_configuration_name="my-hnsw-config")],
        algorithms=[{"name": "my-hnsw-config", "kind": "hnsw"}]
    )
    index = SearchIndex(name=index_name, fields=fields, vector_search=vector_search)
    index_client.create_or_update_index(index)
    print("Índice criado com sucesso.")

def get_all_page_paths(pages):
    paths = []
    for page in pages:
        if 'path' in page:
            paths.append(page['path'])
        if 'subPages' in page:
            paths.extend(get_all_page_paths(page['subPages']))
    return paths

def get_wiki_pages():
    print("Buscando páginas no Azure DevOps Wiki...")
    url = f"https://dev.azure.com/{devops_org}/{devops_project}/_apis/wiki/wikis/{devops_wiki_name}/pages?recursionLevel=full&api-version=6.0"
    pat_b64 = base64.b64encode(f":{devops_pat}".encode('utf-8')).decode('utf-8')
    headers = {'Authorization': f'Basic {pat_b64}'}
    response = requests.get(url, headers=headers)
    response.raise_for_status()
    response_data = response.json()
    page_structure = response_data.get('value', response_data)
    if not isinstance(page_structure, list):
        page_structure = [page_structure]
    paths = get_all_page_paths(page_structure)
    print(f"Encontradas {len(paths)} páginas de conteúdo no total.")
    return paths

def get_wiki_page_content(path):
    if path.startswith('/'):
        path = path[1:]
    url = f"https://dev.azure.com/{devops_org}/{devops_project}/_apis/wiki/wikis/{devops_wiki_name}/pages?path={path}&includeContent=true&api-version=6.0"
    pat_b64 = base64.b64encode(f":{devops_pat}".encode('utf-8')).decode('utf-8')
    headers = {'Authorization': f'Basic {pat_b64}'}
    response = requests.get(url, headers=headers)
    response.raise_for_status()
    content_html = response.json().get("content", "")
    soup = BeautifulSoup(content_html, "html.parser")
    return soup.get_text()

def ingest_wiki_content():
    page_paths = get_wiki_pages()
    documents_to_upload = []
    for path in page_paths:
        if path == '/':
            continue
        print(f"Processando página: {path}")
        content = get_wiki_page_content(path)
        if not content:
            continue
        chunks = content.split('\n\n')
        for i, chunk in enumerate(chunks):
            clean_chunk = " ".join(chunk.strip().split())
            if len(clean_chunk) < 100:
                continue
            print(f"  Gerando embedding para o chunk {i+1} da página '{path}'...")
            embedding_response = openai_client.embeddings.create(input=[clean_chunk], model=embedding_deployment_name)
            embedding_vector = embedding_response.data[0].embedding
          
            safe_path = path.lstrip('/').replace('/', '_').replace('.', '_')
            
            document = {
                "id": f"{safe_path}-chunk{i+1}",
                "source": f"Wiki: {path}",
                "content": clean_chunk,
                "content_vector": embedding_vector
            }
            documents_to_upload.append(document)
    if documents_to_upload:
        print(f"\nEnviando {len(documents_to_upload)} chunks para o índice '{index_name}'...")
        search_client.upload_documents(documents=documents_to_upload)
        print("Upload concluído com sucesso!")
    else:
        print("Nenhum conteúdo válido foi encontrado no Wiki para processar.")

if __name__ == "__main__":
    create_search_index()
    ingest_wiki_content()