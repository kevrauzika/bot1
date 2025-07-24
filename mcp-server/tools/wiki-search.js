import axios from 'axios';
import { decode } from 'html-entities';

export const wikiSearchTool = {
    name: 'wiki_search',
    description: 'Busca informações no Azure DevOps Wiki',
    inputSchema: {
        type: 'object',
        properties: {
            query: {
                type: 'string',
                description: 'Termo de busca no wiki',
            },
            limit: {
                type: 'number',
                description: 'Número máximo de resultados',
                default: 5,
            },
        },
        required: ['query'],
    },
};

export async function searchWikiPages(query, limit = 5) {
    const devopsOrg = process.env.AZURE_DEVOPS_ORG;
    const devopsProject = process.env.AZURE_DEVOPS_PROJECT;
    const devopsPat = process.env.AZURE_DEVOPS_PAT;
    const wikiName = process.env.AZURE_DEVOPS_WIKI_NAME;

    const auth = Buffer.from(`:${devopsPat}`).toString('base64');

    try {
        // Buscar páginas do wiki
        const response = await axios.get(
            `https://dev.azure.com/${devopsOrg}/${devopsProject}/_apis/wiki/wikis/${wikiName}/pages?recursionLevel=full&api-version=6.0`,
            {
                headers: {
                    'Authorization': `Basic ${auth}`,
                },
            }
        );

        // Filtrar e ranquear resultados baseado na query
        const pages = extractAllPages(response.data.value || []);
        const filteredPages = pages
            .filter(page =>
                page.path.toLowerCase().includes(query.toLowerCase()) ||
                (page.content && page.content.toLowerCase().includes(query.toLowerCase()))
            )
            .slice(0, limit);

        return filteredPages.map(page => ({
            path: page.path,
            title: page.path.split('/').pop(),
            relevance: calculateRelevance(page, query),
            preview: page.content ? page.content.substring(0, 200) + '...' : '',
        }));
    } catch (error) {
        throw new Error(`Erro ao buscar no wiki: ${error.message}`);
    }
}

function extractAllPages(pages) {
    let allPages = [];
    for (const page of pages) {
        if (page.path) {
            allPages.push(page);
        }
        if (page.subPages) {
            allPages = allPages.concat(extractAllPages(page.subPages));
        }
    }
    return allPages;
}

function calculateRelevance(page, query) {
    const queryLower = query.toLowerCase();
    let score = 0;

    if (page.path.toLowerCase().includes(queryLower)) score += 10;
    if (page.content && page.content.toLowerCase().includes(queryLower)) score += 5;

    return score;
}