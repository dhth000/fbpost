import { useState } from 'react';
import './App.css';

function App() {
    const [loading, setLoading] = useState(false);
    const [message, setMessage] = useState('');
    const [error, setError] = useState('');

    const runRequest = async (url: string, actionName: string) => {
        setLoading(true);
        setMessage('');
        setError('');

        try {
            const response = await fetch(url, 	);

            if (!response.ok) {
                throw new Error(`Failed to ${actionName.toLowerCase()}.`);
            }

            const contentType = response.headers.get('content-type') || '';
            const payload = contentType.includes('application/json')
                ? await response.json()
                : await response.text();

            if (typeof payload === 'string' && payload) {
                setMessage(payload);
            } else if (payload && typeof payload === 'object') {
                setMessage(JSON.stringify(payload));
            } else {
                setMessage(`${actionName} completed successfully.`);
            }
        } catch (err) {
            setError(err instanceof Error ? err.message : `Unable to ${actionName.toLowerCase()}.`);
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="app-shell">
            <h1>Facebook Post Tool</h1>
            <p>Use the buttons below to upload your data or process a Facebook post.</p>

            <div className="button-group">
                <button type="button" onClick={() => runRequest('/api/facebook/UploadDataFileAsync', 'Upload Data')} disabled={loading}>
                    {loading ? 'Processing...' : 'Upload Data'}
                </button>
                <button type="button" onClick={() => runRequest('/api/facebook/CreateFacebookPostAsync', 'Process FB Post')} disabled={loading}>
                    {loading ? 'Processing...' : 'Process FB Post'}
                </button>
            </div>

            {message && <p className="success-message">{message}</p>}
            {error && <p className="error-message">{error}</p>}
        </div>
    );
}

export default App;