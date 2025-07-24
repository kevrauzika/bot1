import axios from 'axios';

export const devopsIntegrationTool = {
    name: 'devops_integration',
    description: 'Integração com Azure DevOps para work items',
    inputSchema: {
        type: 'object',
        properties: {
            action: {
                type: 'string',
                enum: ['get_work_items', 'create_work_item'],
                description: 'Ação a ser executada',
            },
            query: {
                type: 'string',
                description: 'Query para buscar work items',
            },
            title: {
                type: 'string',
                description: 'Título do work item (para criação)',
            },
            description: {
                type: 'string',
                description: 'Descrição do work item (para criação)',
            },
            type: {
                type: 'string',
                description: 'Tipo do work item (Bug, Task, User Story)',
                default: 'Task',
            },
        },
        required: ['action'],
    },
};

export async function getWorkItems(query) {
    const devopsOrg = process.env.AZURE_DEVOPS_ORG;
    const devopsProject = process.env.AZURE_DEVOPS_PROJECT;
    const devopsPat = process.env.AZURE_DEVOPS_PAT;

    const auth = Buffer.from(`:${devopsPat}`).toString('base64');

    try {
        const wiql = {
            query: `SELECT [System.Id], [System.Title], [System.State], [System.WorkItemType] 
              FROM WorkItems 
              WHERE [System.Title] CONTAINS '${query}' 
              ORDER BY [System.ChangedDate] DESC`
        };

        const response = await axios.post(
            `https://dev.azure.com/${devopsOrg}/${devopsProject}/_apis/wit/wiql?api-version=6.0`,
            wiql,
            {
                headers: {
                    'Authorization': `Basic ${auth}`,
                    'Content-Type': 'application/json',
                },
            }
        );

        return response.data.workItems || [];
    } catch (error) {
        throw new Error(`Erro ao buscar work items: ${error.message}`);
    }
}

export async function createWorkItem(title, description, type = 'Task') {
    const devopsOrg = process.env.AZURE_DEVOPS_ORG;
    const devopsProject = process.env.AZURE_DEVOPS_PROJECT;
    const devopsPat = process.env.AZURE_DEVOPS_PAT;

    const auth = Buffer.from(`:${devopsPat}`).toString('base64');

    try {
        const workItem = [
            {
                op: 'add',
                path: '/fields/System.Title',
                value: title,
            },
            {
                op: 'add',
                path: '/fields/System.Description',
                value: description,
            },
        ];

        const response = await axios.post(
            `https://dev.azure.com/${devopsOrg}/${devopsProject}/_apis/wit/workitems/$${type}?api-version=6.0`,
            workItem,
            {
                headers: {
                    'Authorization': `Basic ${auth}`,
                    'Content-Type': 'application/json-patch+json',
                },
            }
        );

        return response.data;
    } catch (error) {
        throw new Error(`Erro ao criar work item: ${error.message}`);
    }
}